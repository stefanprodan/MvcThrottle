using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace MvcThrottle
{
    [Serializable]
    public class ThrottleLogEntry
    {
        public string RequestId { get; set; }
        public string ClientIp { get; set; }
        public string ClientKey { get; set; }
        public string Endpoint { get; set; }
        public string UserAgent { get; set; }
        public long TotalRequests { get; set; }
        public DateTime StartPeriod { get; set; }
        public long RateLimit { get; set; }
        public string RateLimitPeriod { get; set; }
        public DateTime LogDate { get; set; }
        public HttpRequestBase Request { get; set; }
    }
}
