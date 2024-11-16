using System.Reflection;

namespace CacheLily
{
    public interface ICacheable
    {
        public int CacheCode { get; set; }
        public int GetCacheCode()
        {
            return CacheCode == 0 ? (CacheCode = GenerateCacheHashCode(this)) : CacheCode;
        }
        public static int GenerateCacheHashCode(params object[] values)
        {
            return CacheHashCodeGenerator.GenerateCacheHashCode(values);
        }
   

        public static int GenerateCacheHashCode(ICacheable obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            int hash = 17;
            System.Reflection.PropertyInfo[] properties = obj.GetType().GetProperties();

            foreach (System.Reflection.PropertyInfo property in properties)
            {
                //skipping the CacheCode 
                if (property.Name == nameof(CacheCode))
                {
                    continue;
                }

                object? value = property.GetValue(obj);
                hash = (hash * 31) + (value?.GetHashCode() ?? 0);
            }
            if (hash <= 0)
            {
                hash = -hash;
            }
            return hash;
        }
    }


}
