using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheLily.Attributes
{
    //The caching thing will skip and wont cache anything on this
    [AttributeUsage(AttributeTargets.Method)]
    public class NoCachingAttribute : Attribute;

}
