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
        public void AddPattern(IPatternRule patternrules)
        {
            _predictor.AddPattern(patternrules);
        }
        private (bool found, T value) TryGet(int cacheCode)
        {
            var item = CacheItems.FirstOrDefault(x => x.IsNotNullOrDefault() && x.CacheCode == cacheCode);
            if (item.IsNotNullOrDefault() && !item.IsExpired())
            {
                item.TTL--;
                return (true, item.TValue);
            }
            return (false, default)!;
        }


        private void AddOrUpdate(T item)
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

        private int GetExpiredOrCloseToExpired()
        {
            CacheItem<T>? item = CacheItems.OrderBy(x => x.TTL).FirstOrDefault();
            return item is null ? 0 : Array.IndexOf(CacheItems, item);
        }

        private void CleanupExpiredItems()
        {
            for (int i = 0; i < CacheItems.Length; i++)
            {
                if (CacheItems[i].IsNotNullOrDefault() && CacheItems[i].IsExpired())
                {
                    CacheItems[i] = default;
                }
            }
        }
        /// <summary>
        ///  Creates a new object
        /// </summary>
        /// <param name="obj">object</param>
        /// <returns>returns a ref or a copy</returns>
        /// <exception cref="Exception">Not Match. T must be ICacheable</exception>
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
        /// <summary>
        ///  Creates an object or  a copy of the cached object
        /// </summary>
        /// <param name="obj">the object</param>
        /// <returns>Copy of the object</returns>
        /// <exception cref="Exception">Not Match. T must be ICacheable</exception>
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

        /// <summary>
        /// See <see cref="Invoke{TResult}(Delegate, object[])"/>
        /// </summary>
       
        public T Invoke(Type Object,Delegate func, params object[] args)
        {
            return Invoke<T>(Object,func, args);
        }
        /// <summary>
        ///  Invokes the method
        /// </summary>
        /// <typeparam name="TResult">the expected returned value</typeparam>
        /// <param name="func">Delegate</param>
        /// <param name="args">args</param>
        /// <returns>the returned value and it is also cached</returns>
        /// <exception cref="InvalidCastException">Invalid TResult</exception>
        public TResult Invoke<TResult>(Type Object,Delegate func, params object[] args) where TResult : T
        {
            int hashcode = ICacheable.GenerateCacheHashCode(func.Method.Name, args);
            hashcode = hashcode < 0 ? -hashcode : hashcode;
            dynamic result;
            var m = func.GetMethodInfo();
            if (m.GetCustomAttribute<NoCachingAttribute>() is not null|Object.GetCustomAttribute<NoCachingAttribute>() is not null )
            {
                return ConvertToCacheObjectOrAny<TResult>(func.DynamicInvoke(args)!);
            }
            
            if (Any(hashcode) is var cacheResult && cacheResult.any)
            {
                return (TResult)CacheItems[cacheResult.TValue_Index].TValue;
            }
            if (PredictiveMode
                && args != null
                && args.All(arg => arg is int || arg is string || arg.GetType().IsValueType)
                && m.GetCustomAttribute<NoPredictingAttribute>() is null
                && _predictor.TryPredict(m.Name, args, out result))
            {
                return ConvertToCacheObjectOrAny<TResult>(result);
            }

            //vyzov functions tolko odin raz dlya unikalnogo vvoda

            result = ConvertToCacheObjectOrAny<TResult>(func.DynamicInvoke(args));

            //uchites na resultate vieu predictive regime
            if (PredictiveMode && m.GetCustomAttribute<NoPredictingAttribute>() is  null)
            {
                _predictor.Learn(m.Name, args, typeof(TResult) == typeof(CacheObject) || typeof(TResult) == typeof(CacheObject<object>) ? Cache.DeepSearchValue(result.Value) : result);
            }

            // keshirovaniye resultata
            CacheItems[Index = GetExpiredOrCloseToExpired()] = new CacheItem<T>
            {
                CacheCode = hashcode,
                TValue = result,
                TTL = _expireAfterCalls
            };
            return result;
        }

        /// <summary>
        /// Checks if the delegate is void or not
        /// </summary>
        /// <param name="delegate"></param>
        /// <returns></returns>
        public static bool NotVoidl(Delegate @delegate)
        {
            return @delegate.GetMethodInfo().ReturnType != typeof(void);
        }
        /// <summary>
        ///  Convert the returned value 
        /// </summary>
        /// <typeparam name="TResult">the Type Expected</typeparam>
        /// <param name="value">the object that is converted or not converted</param>
        /// <returns>the converted object</returns>
        /// <exception cref="InvalidCastException"></exception>
        protected TResult ConvertToCacheObjectOrAny<TResult>(object value) where TResult : T
        {
            if (value is TResult castValue)
                return castValue;

            Type targetType;
            if (value is null)
                return default(TResult);
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

        /// <summary>
        ///  Checks if the cachecode exist 
        /// </summary>
        /// <param name="cacheCode">the cacheCode</param>
        /// <returns>any and the TValue index</returns>
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
        /// <summary>
        ///See <see cref="Pin{Timpl}(ref ICacheable)"/>.
        /// </summary>
      
        public int Pin(ref ICacheable cacheable)
        {
            return Pin<T>(ref cacheable);
        }

        /// <summary>
        ///  Pin an ICacheable object to the Cache memory
        /// </summary>
        /// <typeparam name="Timpl">the type of the ICacheble object</typeparam>
        /// <param name="cacheable">The ICacheable object to pin </param>
        /// <returns>returns the ICacheable index in the i cache memory</returns>
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
                CacheItems[Index] = (CacheItem<T>)newItem;
                return Index;
            }
            CacheItems[Index] = (CacheItem<T>)cacheable;
            return Index;
        }


    }
    public class Cache(int Capacity, int TTL = 20, bool PredictiveMode = true) : Cache<CacheObject<dynamic?>>(Capacity, TTL, PredictiveMode)
    {
        /// <summary>
        /// <see cref="Cache{}.Invoke(Delegate, object[])"/>
        /// </summary>

        public new object? Invoke(Delegate func, params object[] args)
        {
            return this.Invoke<object>(func, args) ?? default(object);
        }
        /// <summary>
        /// <see cref="Cache{}.Invoke{}(Delegate, object[])"/>
        /// </summary>
        public T Invoke<T>(Delegate func, params object[] args) 
        {
            if(typeof(T) == GetType())
            {
                throw new InvalidOperationException("T cannot be CacheObject or CacheObject<T>.");

            }
            return (T)DeepSearchValue(base.Invoke(GetType(),func, args))!;
          
        }
        public static dynamic? DeepSearchValue(CacheObject obj)
        {
            if (obj is null)
                return default;
            if (obj.Value is CacheObject e)
                return DeepSearchValue(e);
            else if (obj.Value is CacheObject<dynamic> ex)
                return DeepSearchValue(ex);
            return obj.Value;
        }
        public static dynamic? DeepSearchValue<T>(CacheObject<T> obj)
        {
            if (obj is null)
                return default;
            if (obj.Value is CacheObject<object>  e)
                return DeepSearchValue(e);
            else    if(obj.Value is CacheObject ed)
                return DeepSearchValue(ed);
            return obj.Value;
        }
    }

}