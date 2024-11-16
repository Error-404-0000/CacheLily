using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheLily
{
    public class CacheObject<T> : ICacheable
    {
        public CacheObject()
        {

        }
      
        public CacheObject(T @default)
        {
            Value = @default;
        }
        public int CacheCode { get; set; }
        public T? Value { get; set; }
        public static implicit operator CacheObject<T>(T value)
        {
            return new CacheObject<T>()
            {
                Value = value,
            };
        }
     
        public static implicit operator T(CacheObject<T> cache)
        {
            return cache.Value ?? default(T)!;
        }

        public override string ToString()
        {
            return Value?.ToString()!;
        }
    
        public bool? Equals(CacheObject<T> cache)
        {
            return this.Value?.Equals(cache.Value);
        }
        public static bool operator ==(CacheObject<T> ca1, CacheObject<T> ca2)
        {
            return ca1?.Equals(ca2)??false;
        }

        public static bool operator !=(CacheObject<T> ca1, CacheObject<T> ca2)
        {
            return (!ca1?.Equals(ca2) ?? true);
        }
        public static explicit  operator CacheObject<T>(CacheObject e)
        {
            return new CacheObject<T>()
            {
                Value = (T)e.Value!
            };
        }

    }
    public class CacheObject : CacheObject<object>
    {
        
        public CacheObject(object @default) : base((object)@default)
        {

        }
        public CacheObject()
        {

        }
    }
}
