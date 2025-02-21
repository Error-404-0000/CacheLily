using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheLily.Predictor
{
    public class PredictiveCache
    {
        private readonly IPatternPredictor _predictor;

        public PredictiveCache(IPatternPredictor predictor)
        {
            _predictor = predictor;
        }

        public object PredictOrInvoke(string methodName, Func<object[], object> method, params object[] args)
        {
            if (_predictor.TryPredict(methodName, args,out object result))
            {
                return result;
            }
            //eto pozvolyayet emu uchitsya
            return result;
        }
    }
}
