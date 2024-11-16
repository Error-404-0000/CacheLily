using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheLily.Predictor
{
    public interface IPatternPredictor
    {
        bool TryPredict(string methodName, object[] args, out object result);
        void Learn(string methodName, object[] args, object result);
        void AddPattern( IPatternRule pattern);
    }

}
