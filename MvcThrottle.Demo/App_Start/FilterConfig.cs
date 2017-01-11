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
                Policy = new MvcThrottle.ThrottlePolicy(perSecond: 2, perMinute: 10, perHour: 60 * 10, perDay: 600 * 10)
                {
                    //scope to IPs
                    IpThrottling = true,
                    IpRules = new Dictionary<string, MvcThrottle.RateLimits>
                    { 
                        { "::1/10", new MvcThrottle.RateLimits { PerHour = 15 } },
                        { "192.168.2.1", new MvcThrottle.RateLimits { PerMinute = 30, PerHour = 30*60, PerDay = 30*60*24 } }
                    },
                    IpWhitelist = new List<string> 
                    {
                        //localhost
                        // "::1",
                        "127.0.0.1",
                        //Intranet
                        "192.168.0.0 - 192.168.255.255",
                        //Googlebot - update from http://iplists.com/nw/google.txt                    
                        "64.68.1 - 64.68.255",
                        "64.68.0.1 - 64.68.255.255",
                        "64.233.0.1 - 64.233.255.255",
                        "66.249.1 - 66.249.255",
                        "66.249.0.1 - 66.249.255.255",                        
                        "209.85.0.1 - 209.85.255.255",
                        "209.185.1 - 209.185.255",
                        "216.239.1 - 216.239.255",
                        "216.239.0.1 - 216.239.255.255",
                        //Bingbot                 
                        "65.54.0.1 - 65.54.255.255",
                        "68.54.1 - 68.55.255",
                        "131.107.0.1 - 131.107.255.255",
                        "157.55.0.1 - 157.55.255.255",
                        "202.96.0.1 - 202.96.255.255",
                        "204.95.0.1 - 204.95.255.255",
                        "207.68.1 - 207.68.255",
                        "207.68.0.1 - 207.68.255.255",
                        "219.142.0.1 - 219.142.255.255",
                        //Yahoo - update from http://user-agent-string.info/list-of-ua/bot-detail?bot=Yahoo!
                        "67.195.0.1 - 67.195.255.255",
                        "72.30.0.1 - 72.30.255.255",
                        "74.6.0.1 - 74.6.255.255",
                        "98.137.0.1 - 98.137.255.255",
                        //Yandex - update from http://user-agent-string.info/list-of-ua/bot-detail?bot=YandexBot
                        "100.43.0.1 - 100.43.255.255",
                        "178.154.0.1 - 178.154.255.255",
                        "199.21.0.1 - 199.21.255.255",
                        "37.140.0.1 - 37.140.255.255",
                        "5.255.0.1 - 5.255.255.255",
                        "77.88.0.1 - 77.88.255.255",
                        "87.250.0.1 - 87.250.255.255",
                        "93.158.0.1 - 93.158.255.255",
                        "95.108.0.1 - 95.108.255.255",
                    },

                    //scope to clients
                    ClientThrottling = true,
                    //white list authenticated clients
                    ClientWhitelist = new List<string> { "auth" },

                    //scope to requests path
                    EndpointThrottling = true,
                    EndpointType = EndpointThrottlingType.ControllerAndAction,
                    EndpointRules = new Dictionary<string, RateLimits>
                    { 
                        { "home/", new RateLimits { PerHour = 90 } },
                        { "Home/about", new RateLimits { PerHour = 30 } }
                    }
                },
                IpAddressParser = new NginxIpAddressParser(),
                Logger = new MvcThrottleCustomLogger()
            };

            filters.Add(throttleFilter);
        }
    }
}
