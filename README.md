MvcThrottle
===========

[![Build status](https://ci.appveyor.com/api/projects/status/xdyd4xb4bihivdjt?svg=true)](https://ci.appveyor.com/project/stefanprodan/mvcthrottle)
[![NuGet](https://img.shields.io/nuget/v/MvcThrottle.svg)](https://www.nuget.org/packages/MvcThrottle)

With MvcThrottle you can protect your site from aggressive crawlers, scraping tools or unwanted traffic spikes originated from the same location by limiting the rate of requests that a client from the same IP can make to your site or to specific routes.

You can set multiple limits for different scenarios like allowing an IP to make a maximum number of calls per second, per minute, per hour or per day. You can define these limits to address all requests made to your website or you can scope the limits to each Controller, Action or URL, with or without query string params.

### Global throttling based on IP

The setup bellow will limit the number of requests originated from the same IP. 
If from the same IP, in same second, you'll make a call to <code>home/index</code> and <code>home/about</code> the last call will get blocked.

``` cs
public class FilterConfig
{
    public static void RegisterGlobalFilters(GlobalFilterCollection filters)
    {
        var throttleFilter = new ThrottlingFilter
        {
            Policy = new ThrottlePolicy(perSecond: 1, perMinute: 10, perHour: 60 * 10, perDay: 600 * 10)
            {
                IpThrottling = true
            },
            Repository = new CacheRepository()
        };

        filters.Add(throttleFilter);
    }
}
```

In order to enable throttling you'll have to decorate your Controller or Action with <code>EnableThrottlingAttribute</code>, if you want to exclude a certain Action you can apply <code>DisableThrottlingAttribute</code>.

``` cs
[EnableThrottling]
public class HomeController : Controller
{
    public ActionResult Index()
    {
        return View();
    }

    [DisableThrottling]
    public ActionResult About()
    {
        return View();
    }
}
```

You can define custom limits using the EnableThrottling attribute, these limits will override the default ones.

``` cs
[EnableThrottling(PerSecond = 2, PerMinute = 10, PerHour = 30, PerDay = 300)]
public ActionResult Index()
{
    return View();
}
```

### Endpoint throttling based on IP

If, from the same IP, in the same second, you'll make two calls to <code>home/index</code>, the last call will get blocked.
But if in the same second you call <code>home/about</code> too, the request will go through because it's a different route.

``` cs
var throttleFilter = new ThrottlingFilter
{
    Policy = new ThrottlePolicy(perSecond: 1, perMinute: 10, perHour: 60 * 10, perDay: 600 * 10)
    {
        IpThrottling = true,
        EndpointThrottling = true,
        EndpointType = EndpointThrottlingType.ControllerAndAction
    },
    Repository = new CacheRepository()
};
```

Using the <code>ThrottlePolicy.EndpointType</code> property you can chose how the throttle key gets compose.

``` cs
public enum EndpointThrottlingType
{
    AbsolutePath = 1,
    PathAndQuery,
    ControllerAndAction,
    Controller
}
```

### Customizing the rate limit response

By default, when a client is rate limited a 429 HTTP status code is sent back along with <code>Retry-After</code> header. If you want to return a custom view instead of IIS error page you’ll need to implement your own ThrottlingFilter and override the <code>QuotaExceededResult</code> method.

``` cs
public class MvcThrottleCustomFilter : MvcThrottle.ThrottlingFilter
{
    protected override ActionResult QuotaExceededResult(RequestContext context, string message, HttpStatusCode responseCode)
    {
        var rateLimitedView = new ViewResult
        {
            ViewName = "RateLimited"
        };
        rateLimitedView.ViewData["Message"] = message;

        return rateLimitedView;
    }
}
```

I’ve created a view named RateLimited.cshtml located in the Views/Shared folder and using ViewBag.Message I am sending the error message to this view. Take a look at MvcThrottle.Demo project for the full implementation.

### IP, Endpoint and Client White-listing

If requests are initiated from a white-listed IP or to a white-listed URL, then the throttling policy will not be applied and the requests will not get stored. The IP white-list supports IP v4 and v6 ranges like "192.168.0.0/24", "fe80::/10" and "192.168.0.0-192.168.0.255" for more information check [jsakamoto/ipaddressrange](https://github.com/jsakamoto/ipaddressrange).

``` cs
var throttleFilter = new ThrottlingFilter
{
	Policy = new ThrottlePolicy(perSecond: 2, perMinute: 60)
	{
		IpThrottling = true,
		IpWhitelist = new List<string> { "::1", "192.168.0.0/24" },
		
		EndpointThrottling = true,
		EndpointType = EndpointThrottlingType.ControllerAndAction,
		EndpointWhitelist = new List<string> { "Home/Index" },
		
		ClientThrottling = true,
		//white list authenticated users
		ClientWhitelist = new List<string> { "auth" }
	},
	Repository = new CacheRepository()
});
```

The Demo project comes with a white-list of Google and Bing bot IPs, take at look at  [FilterConfig.cs](https://github.com/stefanprodan/MvcThrottle/blob/master/MvcThrottle.Demo/App_Start/FilterConfig.cs).

### IP and/or Endpoint custom rate limits

You can define custom limits for known IPs and endpoint, these limits will override the default ones. 
Be aware that a custom limit will only work if you have defined a global counterpart.
You can define endpoint rules by providing relative routes like <code>Home/Index</code> or just a URL segment like <code>/About/</code>. 
The endpoint throttling engine will search for the expression you've provided in the absolute URI, 
if the expression is contained in the request route then the rule will be applied. 
If two or more rules match the same URI then the lower limit will be applied.

``` cs
var throttleFilter = new ThrottlingFilter
{
	Policy = new ThrottlePolicy(perSecond: 1, perMinute: 20, perHour: 200, perDay: 1500)
	{
		IpThrottling = true,
		IpRules = new Dictionary<string, RateLimits>
		{ 
			{ "192.168.1.1", new RateLimits { PerSecond = 2 } },
			{ "192.168.2.0/24", new RateLimits { PerMinute = 30, PerHour = 30*60, PerDay = 30*60*24 } }
		},
		
		EndpointThrottling = true,
		EndpointType = EndpointThrottlingType.ControllerAndAction,
		EndpointRules = new Dictionary<string, RateLimits>
		{ 
			{ "Home/Index", new RateLimits { PerMinute = 40, PerHour = 400 } },
			{ "Home/About", new RateLimits { PerDay = 2000 } }
		}
	},
	Repository = new CacheRepository()
});
```

### User-Agent rate limiting

You can define custom limits for known User-Agents or event white-list them, these limits will override the default ones. 

``` cs
var throttleFilter = new ThrottlingFilter
{
	Policy = new ThrottlePolicy(perSecond: 5, perMinute: 20, perHour: 200, perDay: 1500)
	{
		IpThrottling = true,
		EndpointThrottling = true,
		EndpointType = EndpointThrottlingType.AbsolutePath,

		UserAgentThrottling = true,
		UserAgentWhitelist = new List<string>
		{
			"Googlebot",
			"Mediapartners-Google",
			"AdsBot-Google",
			"Bingbot",
			"YandexBot",
			"DuckDuckBot"
		},
		UserAgentRules = new Dictionary<string, RateLimits>
		{
			{"Slurp", new RateLimits { PerMinute = 1 }},
			{"Sogou", new RateLimits { PerHour = 1 } }
		}
	},
	Repository = new CacheRepository()
});
```

The above setup will allow the Sogou bot to crawl each URL once every hour while Google, Bing, Yandex and DuckDuck will not get rate limited at all. 
Any other bot that is not present in the setup will be rate limited based on the global rules defined in the ThrottlePolicy constuctor.

### Stack rejected requests

By default, rejected calls are not added to the throttle counter. If a client makes 3 requests per second 
and you've set a limit of one call per second, the minute, hour and day counters will only record the first call, the one that wasn't blocked.
If you want rejected requests to count towards the other limits, you'll have to set <code>StackBlockedRequests</code> to true.

``` cs
var throttleFilter = new ThrottlingFilter
{
	Policy = new ThrottlePolicy(perSecond: 1, perMinute: 30)
	{
		IpThrottling = true,
		EndpointThrottling = true,
		StackBlockedRequests = true
	},
	Repository = new CacheRepository()
});
```

### Storing throttle metrics 

MvcThrottle stores all request data in-memory using ASP.NET Cache. If you want to change the storage to 
Velocity, MemCache or Redis, all you have to do is create your own repository by implementing the IThrottleRepository interface. 

``` cs
public interface IThrottleRepository
{
	bool Any(string id);
	
	ThrottleCounter? FirstOrDefault(string id);
	
	void Save(string id, ThrottleCounter throttleCounter, TimeSpan expirationTime);
	
	void Remove(string id);
	
	void Clear();
}
```

### Logging throttled requests

If you want to log throttled requests you'll have to implement IThrottleLogger interface and provide it to the ThrottlingFilter. 

``` cs
public interface IThrottleLogger
{
	void Log(ThrottleLogEntry entry);
}
```

Logging implementation example
``` cs
public class MvcThrottleLogger : IThrottleLogger
{
    public void Log(ThrottleLogEntry entry)
    {
        Debug.WriteLine("{0} Request {1} from {2} has been blocked, quota {3}/{4} exceeded by {5}",
            entry.LogDate, entry.RequestId, entry.ClientIp, entry.RateLimit, entry.RateLimitPeriod, entry.TotalRequests);
    }
}
```

Logging usage example 
``` cs
var throttleFilter = new ThrottlingFilter
{
	Policy = new ThrottlePolicy(perSecond: 1, perMinute: 30)
	{
		IpThrottling = true,
		EndpointThrottling = true
	},
	Repository = new CacheRepository(),
	Logger = new DebugThrottleLogger()
});
```
