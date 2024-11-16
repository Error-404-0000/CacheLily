namespace CacheLily.Predictor
{
    public interface IPatternRule
    {
        bool Matches(byte[] memoryBytes, out object result);
        void Learn(byte[] memoryBytes, object result);
    }

}
