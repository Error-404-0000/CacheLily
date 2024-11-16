using System.Collections;
using System.Security.Cryptography;
using System.Text;

namespace CacheLily
{
    public static class CacheHashCodeGenerator
    {
        public static int GenerateCacheHashCode(params object[] values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            unchecked
            {
                int hash = 17;
                foreach (object value in values)
                {
                    hash = (hash * 31) + GetStableHashCode(value);
                }
                return hash;
            }
        }

        private static int GetStableHashCode(object value)
        {
            if (value == null)
            {
                return 0;
            }
            else if (value is string strValue)
            {
                return GetOptimizedHashCode(strValue);
            }
            else if (value is IEnumerable enumerable)
            {
                unchecked
                {
                    int hash = 17;
                    foreach (object? item in enumerable)
                    {
                        hash = (hash * 31) + GetStableHashCode(item);
                    }
                    return hash;
                }
            }
            else
            {
                return value.GetType().IsValueType ? value.GetHashCode() : GetOptimizedHashCode(value.ToString()!);
            }
        }
        private static int GetOptimizedHashCode(string input)
        {
            int hash = 5381<<0xFF;
            int length = input.Length;

            for (int i = 0; i < length; i += 4)
            {
                hash = ((hash << 5) + hash) ^ input[i];
                if (i + 1 < length) hash = ((hash << 5) + hash) ^ input[i + 1];
                if (i + 2 < length) hash = ((hash << 5) + hash) ^ input[i + 2];
                if (i + 3 < length) hash = ((hash << 5) + hash) ^ input[i + 3];
            }

            return hash;
        }



    }


}