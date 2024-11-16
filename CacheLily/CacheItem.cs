namespace CacheLily
{
    public struct CacheItem<T> : ICacheable where T : ICacheable
    {
        public int TTL;
        public T TValue;
        public int CacheCode { get; set; }
        public readonly bool IsNotNullOrDefault()
        {
            return !IsNull();
        }

        public readonly bool IsNull()
        {
            return TValue is null;
        }
        public bool SubTSO(int index)
        {
            TTL -= index;
            return true;
        }
        public readonly bool CacheEquals(int _CacheCode)
        {
            return CacheCode == _CacheCode;
        }

        public readonly bool IsExpired()
        {
            return TTL <= 0;
        }

        public readonly override int GetHashCode()
        {
            return (this as ICacheable).GetCacheCode();
        }

    }


}