using System;
using System.Collections.Generic;
using System.Linq;

namespace CacheLily.Predictor
{
   

    public class OptimizedPatternPredictor : IPatternPredictor
    {
        private readonly List<IPatternRule> _patterns = new(); // Global rule list
        private readonly Dictionary<string, Dictionary<int, object>> _resultCache = new(); // Per-method cache

        public bool TryPredict(string methodName, object[] args, out object result)
        {
            result = null;

            // Check if the method has cached results
            var memoryBytes = GetByteRepresentation(args);
            int hash = ComputeHash(memoryBytes);
            hash = hash < 0 ? -hash : hash;
            if (_resultCache.TryGetValue(methodName, out var methodCache) && methodCache.TryGetValue(hash, out result))
            {
                
                return true;
            }

            // Check global patterns for matches
            foreach (var pattern in _patterns)
            {
                if (pattern.Matches(memoryBytes, out result))
                {
                    CacheResult(methodName, hash, result); 
                    
                    return true;
                }
            }

            return false; // No prediction
        }

        public void Learn(string methodName, object[] args, object result)
        {
            var memoryBytes = GetByteRepresentation(args);
            int hash = ComputeHash(memoryBytes);

            // Cache the result
            CacheResult(methodName, hash, result);

            // Teach global patterns
            foreach (var pattern in _patterns)
            {
                pattern.Learn(memoryBytes, result);
            }
        }

        public void AddPattern(IPatternRule pattern)
        {
            _patterns.Add(pattern); // Add a new rule to the global list
        }

        private void CacheResult(string methodName, int hash, object result)
        {
            if (_resultCache.ContainsKey(methodName)&&_resultCache[methodName].Count>300)
                _resultCache[methodName].Clear();
            if (!_resultCache.ContainsKey(methodName))
            {
                _resultCache[methodName] = new Dictionary<int, object>();
            }

            _resultCache[methodName][hash] = result;
        }

        private int ComputeHash(byte[] bytes)
        {
            return bytes.Aggregate(17, (hash, b) => hash * 31 + b);
        }

        private byte[] GetByteRepresentation(object[] args)
        {
            var bytes = new List<byte>();
            foreach (var arg in args)
            {
                if (arg is int i)
                {
                    bytes.AddRange(BitConverter.GetBytes(i));
                }
                else if (arg is double d)
                {
                    bytes.AddRange(BitConverter.GetBytes(d));
                }
                else if (arg is string s)
                {
                    bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(s));
                }
                else
                {
                    throw new NotSupportedException($"Type {arg.GetType()} not supported for byte representation.");
                }
            }
            return bytes.ToArray();
        }
    }
}
