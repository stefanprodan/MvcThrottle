using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;

namespace MvcThrottle.Demo.Helpers
{
public class MvcThrottleCustomLogger : IThrottleLogger
{
    public void Log(ThrottleLogEntry entry)
    {
        Debug.WriteLine("{0} Request {1} from {2} has been blocked, quota {3}/{4} exceeded by {5}",
            entry.LogDate, entry.RequestId, entry.ClientIp, entry.RateLimit, entry.RateLimitPeriod, entry.TotalRequests);
    }
}
}