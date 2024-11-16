using System;

namespace CacheLily.Predictor
{
    public class MathPatternRule : IPatternRule
    {
        private Func<byte[], object> _ruleFunction;
        private int _confidence;
        private int _relearnThreshold;
        private const int ConfidenceThreshold = 80;

        public MathPatternRule()
        {
            _confidence = 0;
            _relearnThreshold = 5; // Number of mismatches before relearning
        }

        public bool Matches(byte[] memoryBytes, out object result)
        {
            result = null;

            if (_ruleFunction != null && _confidence >= ConfidenceThreshold)
            {
                result = _ruleFunction(memoryBytes);
                return true;
            }

            return false;
        }

        public void Learn(byte[] memoryBytes, object output)
        {
            int expectedOutput = Convert.ToInt32(output);

            if (_ruleFunction == null)
            {
                _ruleFunction = GenerateRule(memoryBytes, expectedOutput);
                _confidence = 10; // Start baseline confidence
            }
            else
            {
                var predictedOutput = _ruleFunction(memoryBytes);
                if (predictedOutput.Equals(expectedOutput))
                {
                    _confidence = Math.Min(_confidence + 20, 100); // Boost confidence
                }
                else
                {
                    _relearnThreshold--;
                    if (_relearnThreshold <= 0)
                    {
                        _ruleFunction = GenerateRule(memoryBytes, expectedOutput); // Refine the rule
                        _confidence = 10; // Reset confidence
                        _relearnThreshold = 5; // Reset relearn threshold
                    }
                    else
                    {
                        _confidence = Math.Max(_confidence - 10, 0); // Decay confidence
                    }
                }
            }
        }

        private Func<byte[], object> GenerateRule(byte[] memoryBytes, int output)
        {
            unsafe
            {
                fixed (byte* ptr = memoryBytes)
                {
                    int a = *(int*)ptr;
                    int b = *(int*)(ptr + 4);

                    if (output == a + b)
                    {
                        return bytes =>
                        {
                            fixed (byte* p = bytes)
                            {
                                return *(int*)p + *(int*)(p + 4);
                            }
                        };
                    }

                    if (output == a * b)
                    {
                        return bytes =>
                        {
                            fixed (byte* p = bytes)
                            {
                                return *(int*)p * *(int*)(p + 4);
                            }
                        };
                    }

                    return _ => output; // Default rule
                }
            }
        }
    }
}
