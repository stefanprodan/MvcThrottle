using MvcThrottle.Demo.Helpers;
using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;

namespace MvcThrottle.Demo
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());

            var throttleFilter = new MvcThrottleCustomFilter
            {
                Policy = new MvcThrottle.ThrottlePolicy(perSecond: 1, perMinute: 10, perHour: 60 * 10, perDay: 600 * 10)
                {
                    //scope to IPs
                    IpThrottling = true,
                    IpRules = new Dictionary<string, MvcThrottle.RateLimits>
                    { 
                        { "::1/10", new MvcThrottle.RateLimits { PerSecond = 2 } },
                        { "192.168.2.1", new MvcThrottle.RateLimits { PerMinute = 30, PerHour = 30*60, PerDay = 30*60*24 } }
                    },
                    //white list the "::1" IP to disable throttling on localhost for Win8
                    IpWhitelist = new List<string> { "127.0.0.1", "192.168.0.0/24" },

                    //scope to clients
                    ClientThrottling = true,
                    ClientRules = new Dictionary<string, RateLimits>
                    { 
                        { "anon", new RateLimits { PerMinute = 15, PerHour = 600 } }
                    },
                    //white list clients that don’t require throttling
                    ClientWhitelist = new List<string> { "auth" },

                    //scope to requests path
                    EndpointThrottling = true,
                    EndpointType = EndpointThrottlingType.ControllerAndAction,
                    EndpointRules = new Dictionary<string, RateLimits>
                    { 
                        { "api/values/", new RateLimits { PerSecond = 3 } },
                        { "api/values", new RateLimits { PerSecond = 4 } }
                    }
                },
                Repository = new CacheRepository(),
                Logger = new Helpers.MvcThrottleLogger()
            };

            filters.Add(throttleFilter);
        }
    }
}
