using CacheLily.Attributes;
using CacheLily.Predictor;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CacheLily
{
    public class Cache<T> where T : ICacheable
    {
        public int Capacity { get; }
        public CacheItem<T>[] CacheItems { get; }
        private int _index;
        private readonly object index_lock = new();
        private int Index
        {
            get => _index;
            set
            {
                lock (index_lock)
                {
                    _index = value;
                }
            }
        }
        private readonly int _expireAfterCalls;
        private (int CacheCode, int index, int exp) last_return = default;
        public static readonly Cache<CacheObject> OtherCache = OtherCache = new Cache<CacheObject>(3, 30);
        private readonly IPatternPredictor _predictor;
        private readonly PredictiveCache _predictiveCache;
        public bool PredictiveMode { get; }

        public Cache(int capacity, int expireAfterCalls = 10, bool predictiveMode = true)
        {
            Capacity = capacity;
            _expireAfterCalls = expireAfterCalls;
            CacheItems = new CacheItem<T>[capacity];
            PredictiveMode = predictiveMode;

            if (PredictiveMode)
            {
                _predictor = new OptimizedPatternPredictor();
                _predictiveCache = new PredictiveCache(_predictor);
                _predictor.AddPattern(new MathPatternRule());
            }
        }

        public (bool found, T value) TryGet(int cacheCode)
        {
            var item = CacheItems.FirstOrDefault(x => x.IsNotNullOrDefault() && x.CacheCode == cacheCode);
            if (item.IsNotNullOrDefault() && !item.IsExpired())
            {
                item.TTL--;
                return (true, item.TValue);
            }
            return (false, default)!;
        }


        public void AddOrUpdate(T item)
        {
            var cacheCode = item.GetCacheCode();
            var cacheItem = new CacheItem<T>
            {
                CacheCode = cacheCode,
                TValue = item,
                TTL = _expireAfterCalls
            };

            Index = GetExpiredOrCloseToExpired();
            CacheItems[Index] = cacheItem;
        }

        public int GetExpiredOrCloseToExpired()
        {
            CacheItem<T>? item = CacheItems.OrderBy(x => x.TTL).FirstOrDefault();
            return item is null ? 0 : Array.IndexOf(CacheItems, item);
        }

        public void CleanupExpiredItems()
        {
            for (int i = 0; i < CacheItems.Length; i++)
            {
                if (CacheItems[i].IsNotNullOrDefault() && CacheItems[i].IsExpired())
                {
                    CacheItems[i] = default;
                }
            }
        }

        public ref T NewRef(T obj)
        {
            if (last_return != default && last_return.CacheCode == obj.GetCacheCode())
            {
                GC.SuppressFinalize(obj);
                return ref CacheItems[last_return.index].TValue;
            }
            if (Any(obj.GetCacheCode()) is var result && result.any)
            {
                GC.SuppressFinalize(obj);
                return ref CacheItems[result.TValue_Index].TValue;
            }
            if (obj is ICacheable cache)
            {
                return ref CacheItems[Pin(ref cache)].TValue;
            }
            else
            {
                throw new Exception("Not Match. T must be ICacheable");
            }
        }

        public T New(T obj)
        {
            if (last_return != default && last_return.CacheCode == obj.GetCacheCode())
            {
                GC.SuppressFinalize(obj);
                return CacheItems[last_return.index].TValue;
            }
            if (Any(obj.GetCacheCode()) is var result && result.any)
            {
                GC.SuppressFinalize(obj);
                return CacheItems[result.TValue_Index].TValue;
            }
            return obj is ICacheable cache ? CacheItems[Pin(ref cache)].TValue : throw new Exception("Not Match. T must be ICacheable");
        }

        public T Invoke(Delegate func, params object[] args)
        {
            return Invoke<T>(func, args);
        }
        public TResult Invoke<TResult>(Delegate func, params object[] args) where TResult : T
        {
            int hashcode = ICacheable.GenerateCacheHashCode(func.Method.Name, args);
            hashcode = hashcode < 0 ? -hashcode : hashcode;
            dynamic result;
            var m = func.GetMethodInfo();
            if (m.GetCustomAttribute<NoCachingAttribute>() is not null)
            {
                return ConvertToCacheObjectOrAny<TResult>(func.DynamicInvoke(args));
            }
            // Predict or use cache if available
            
            // Cache check if prediction fails
            if (Any(hashcode) is var cacheResult && cacheResult.any)
            {
                return (TResult)CacheItems[cacheResult.TValue_Index].TValue;
            }
            if (PredictiveMode && m.GetCustomAttribute<NoPredictingAttribute>() is null && _predictor.TryPredict(m.Name,args, out result))
            {
                return ConvertToCacheObjectOrAny<TResult>(result);
            }

            // Invoke function only once for unique input
            result = ConvertToCacheObjectOrAny<TResult>(func.DynamicInvoke(args));

            // Learn from result in Predictive Mode
            if (PredictiveMode && m.GetCustomAttribute<NoPredictingAttribute>() is  null)
            {
                _predictor.Learn(m.Name, args, typeof(TResult) == typeof(CacheObject) || typeof(TResult) == typeof(CacheObject<object>) ? Cache.DeepSearchValue(result.Value) : result);
            }

            // Cache the result
            CacheItems[Index = GetExpiredOrCloseToExpired()] = new CacheItem<T>
            {
                CacheCode = hashcode,
                TValue = result,
                TTL = _expireAfterCalls
            };
            if (result is TResult tre)
                return tre;
            throw new InvalidCastException(nameof(TResult));
        }


        public static bool NotVoidl(Delegate @delegate)
        {
            return @delegate.GetMethodInfo().ReturnType != typeof(void);
        }
        protected TResult ConvertToCacheObjectOrAny<TResult>(object value) where TResult : T
        {
            if (value is TResult castValue)
                return castValue;

            Type targetType;

            if (typeof(TResult) == typeof(CacheObject) || value.GetType() == typeof(CacheObject))
            {
                targetType = typeof(CacheObject);
            }
            else if (typeof(TResult) == typeof(CacheObject<>) || value.GetType() == typeof(CacheObject<>))
            {
                return (TResult)value;
            }
            else if (typeof(TResult).IsGenericType && typeof(TResult).GetGenericTypeDefinition() == typeof(CacheObject<>))
            {
                targetType = typeof(CacheObject<>).MakeGenericType(typeof(TResult).GetGenericArguments()[0]);
            }
            else
            {
                throw new InvalidCastException($"Cannot convert object of type '{value.GetType()}' to '{typeof(TResult)}'.");
            }

            var obj = Activator.CreateInstance(targetType, [value])
                      ?? throw new InvalidCastException($"Cannot create an instance of type '{targetType}' with the given value.");

            return (TResult)obj;
        }


        public (bool any, int TValue_Index) Any(int cacheCode)
        {
            if (last_return.CacheCode == cacheCode && last_return.exp > 0 && CacheItems[last_return.index].IsNotNullOrDefault())
            {
                CacheItems[last_return.index].TTL = --last_return.exp;
                return (true, last_return.index);
            }

            for (int i = 0; i < CacheItems.Length; i++)
            {
                var item = CacheItems[i];
                if (item.IsNotNullOrDefault() && item.CacheCode == cacheCode)
                {
                    if (item.IsExpired()) return (false, -1);
                    last_return = (cacheCode, i, item.TTL - 1);
                    item.TTL--;
                    return (true, i);
                }
            }
            return (false, -1);
        }

        public int Pin(ref ICacheable cacheable)
        {
            return Pin<T>(ref cacheable);
        }
        public int Pin<Timpl>(ref ICacheable cacheable) where Timpl : T
        {
            Index = GetExpiredOrCloseToExpired();
            if (cacheable is not CacheItem<Timpl>)
            {
                ICacheable newItem = new CacheItem<Timpl>
                {
                    CacheCode = cacheable.GetCacheCode(),
                    TValue = (Timpl)cacheable,
                    TTL = _expireAfterCalls
                };
                GC.SuppressFinalize(cacheable);
                CacheItems[Index] = (CacheItem<T>)cacheable;
                return Index;
            }
            CacheItems[Index] = (CacheItem<T>)cacheable;
            return Index;
        }


    }
    [Obsolete("Not recommended: uses more memory and is slower due to object allocation required to hold the value. Use Cache<T> instead.", error: false)]
    public class Cache(int Capacity, int TTL = 20, bool PredictiveMode = true) : Cache<CacheObject<dynamic?>>(Capacity, TTL, PredictiveMode)
    {



        public new object? Invoke(Delegate func, params object[] args)
        {
            return this.Invoke<object>(func, args) ?? default(object);
        }

        public new T Invoke<T>(Delegate func, params object[] args) 
        {
            if(typeof(T) == GetType())
            {
                throw new InvalidOperationException("T cannot be CacheObject or CacheObject<T>.");

            }
            return (T)DeepSearchValue(base.Invoke(func, args));
          
        }
        public static dynamic? DeepSearchValue(CacheObject obj)
        {
            if(obj.Value is CacheObject e)
                return DeepSearchValue(e);
            else if (obj.Value is CacheObject<dynamic> ex)
                return DeepSearchValue(ex);
            return obj.Value;
        }
        public static dynamic? DeepSearchValue<T>(CacheObject<T> obj)
        {
            if (obj.Value is CacheObject<object>  e)
                return DeepSearchValue(e);
            else    if(obj.Value is CacheObject ed)
                return DeepSearchValue(ed);
            return obj.Value;
        }
    }

}