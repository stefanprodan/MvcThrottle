using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace MvcThrottle
{
    public class ThrottlingFilter : ActionFilterAttribute, IActionFilter
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ThrottlingHandler"/> class.
        /// By default, the <see cref="QuotaExceededResponseCode"/> property 
        /// is set to 429 (Too Many Requests).
        /// </summary>
        public ThrottlingFilter()
        {
            QuotaExceededResponseCode = (HttpStatusCode)429;
            Repository = new CacheRepository();
            IpAddressParser = new IpAddressParser();
        }

        /// <summary>
        /// Throttling rate limits policy
        /// </summary>
        public ThrottlePolicy Policy { get; set; }

        /// <summary>
        /// Throttle metrics storage
        /// </summary>
        public IThrottleRepository Repository { get; set; }

        /// <summary>
        /// Log blocked requests
        /// </summary>
        public IThrottleLogger Logger { get; set; }

        /// <summary>
        /// If none specifed the default will be: 
        /// HTTP request quota exceeded! maximum admitted {0} per {1}
        /// </summary>
        public string QuotaExceededMessage { get; set; }

        /// <summary>
        /// Gets or sets the value to return as the HTTP status 
        /// code when a request is rejected because of the
        /// throttling policy. The default value is 429 (Too Many Requests).
        /// </summary>
        public HttpStatusCode QuotaExceededResponseCode { get; set; }

        public IIpAddressParser IpAddressParser { get; set; }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            EnableThrottlingAttribute attrPolicy = null;
            var applyThrottling = ApplyThrottling(filterContext, out attrPolicy);

            if (Policy != null && applyThrottling)
            {
                var identity = SetIdentity(filterContext.HttpContext.Request);

                if (!IsWhitelisted(identity))
                {
                    TimeSpan timeSpan = TimeSpan.FromSeconds(1);

                    var rates = Policy.Rates.AsEnumerable();
                    if (Policy.StackBlockedRequests)
                    {
                        //all requests including the rejected ones will stack in this order: day, hour, min, sec
                        //if a client hits the hour limit then the minutes and seconds counters will expire and will eventually get erased from cache
                        rates = Policy.Rates.Reverse();
                    }

                    //apply policy
                    //the IP rules are applied last and will overwrite any client rule you might defined
                    foreach (var rate in rates)
                    {
                        var rateLimitPeriod = rate.Key;
                        var rateLimit = rate.Value;

                        switch (rateLimitPeriod)
                        {
                            case RateLimitPeriod.Second:
                                timeSpan = TimeSpan.FromSeconds(1);
                                break;
                            case RateLimitPeriod.Minute:
                                timeSpan = TimeSpan.FromMinutes(1);
                                break;
                            case RateLimitPeriod.Hour:
                                timeSpan = TimeSpan.FromHours(1);
                                break;
                            case RateLimitPeriod.Day:
                                timeSpan = TimeSpan.FromDays(1);
                                break;
                            case RateLimitPeriod.Week:
                                timeSpan = TimeSpan.FromDays(7);
                                break;
                        }

                        //increment counter
                        string requestId;
                        var throttleCounter = ProcessRequest(identity, timeSpan, rateLimitPeriod, out requestId);

                        if (throttleCounter.Timestamp + timeSpan < DateTime.UtcNow)
                            continue;

                        //apply EnableThrottlingAttribute policy
                        var attrLimit = attrPolicy.GetLimit(rateLimitPeriod);
                        if (attrLimit > 0)
                        {
                            rateLimit = attrLimit;
                        }

                        //apply endpoint rate limits
                        if (Policy.EndpointRules != null)
                        {
                            var rules = Policy.EndpointRules.Where(x => identity.Endpoint.IndexOf(x.Key, 0, StringComparison.InvariantCultureIgnoreCase) != -1).ToList();
                            if (rules.Any())
                            {
                                //get the lower limit from all applying rules
                                var customRate = (from r in rules let rateValue = r.Value.GetLimit(rateLimitPeriod) select rateValue).Min();

                                if (customRate > 0)
                                {
                                    rateLimit = customRate;
                                }
                            }
                        }

                        //apply custom rate limit for clients that will override endpoint limits
                        if (Policy.ClientRules != null && Policy.ClientRules.Keys.Contains(identity.ClientKey))
                        {
                            var limit = Policy.ClientRules[identity.ClientKey].GetLimit(rateLimitPeriod);
                            if (limit > 0) rateLimit = limit;
                        }

                        //apply custom rate limit for user agent
                        if (Policy.UserAgentRules != null && !string.IsNullOrEmpty(identity.UserAgent))
                        {
                            var rules = Policy.UserAgentRules.Where(x => identity.UserAgent.IndexOf(x.Key, 0, StringComparison.InvariantCultureIgnoreCase) != -1).ToList();
                            if (rules.Any())
                            {
                                //get the lower limit from all applying rules
                                var customRate = (from r in rules let rateValue = r.Value.GetLimit(rateLimitPeriod) select rateValue).Min();
                                rateLimit = customRate;
                            }
                        }

                        //enforce ip rate limit as is most specific 
                        string ipRule = null;
                        if (Policy.IpRules != null && IpAddressParser.ContainsIp(Policy.IpRules.Keys.ToList(), identity.ClientIp, out ipRule))
                        {
                            var limit = Policy.IpRules[ipRule].GetLimit(rateLimitPeriod);
                            if (limit > 0) rateLimit = limit;
                        }

                        //check if limit is reached
                        if (rateLimit > 0 && throttleCounter.TotalRequests > rateLimit)
                        {
                            //log blocked request
                            if (Logger != null) Logger.Log(ComputeLogEntry(requestId, identity, throttleCounter, rateLimitPeriod.ToString(), rateLimit, filterContext.HttpContext.Request));

                            //break execution and return 409 
                            var message = string.IsNullOrEmpty(QuotaExceededMessage) ?
                                "HTTP request quota exceeded! maximum admitted {0} per {1}" : QuotaExceededMessage;

                            //add status code and retry after x seconds to response
                            filterContext.HttpContext.Response.StatusCode = (int)QuotaExceededResponseCode;
                            filterContext.HttpContext.Response.Headers.Set("Retry-After", RetryAfterFrom(throttleCounter.Timestamp, rateLimitPeriod));

                            filterContext.Result = QuotaExceededResult(
                                filterContext.RequestContext,
                                string.Format(message, rateLimit, rateLimitPeriod),
                                QuotaExceededResponseCode,
                                requestId);
                                
                            return;
                        }
                    }
                }
            }

            base.OnActionExecuting(filterContext);
        }

        protected virtual RequestIdentity SetIdentity(HttpRequestBase request)
        {
            var entry = new RequestIdentity();
            entry.ClientIp = IpAddressParser.GetClientIp(request).ToString();

            entry.ClientKey = request.IsAuthenticated ? "auth" : "anon";

            var rd = request.RequestContext.RouteData;
            string currentAction = rd.GetRequiredString("action");
            string currentController = rd.GetRequiredString("controller");

            switch (Policy.EndpointType)
            {
                case EndpointThrottlingType.AbsolutePath:
                    entry.Endpoint = request.Url.AbsolutePath;
                    break;
                case EndpointThrottlingType.PathAndQuery:
                    entry.Endpoint = request.Url.PathAndQuery;
                    break;
                case EndpointThrottlingType.ControllerAndAction:
                    entry.Endpoint = currentController + "/" + currentAction;
                    break;
                case EndpointThrottlingType.Controller:
                    entry.Endpoint = currentController;
                    break;
                default:
                    break;
            }

            //case insensitive routes
            entry.Endpoint = entry.Endpoint.ToLowerInvariant();

            entry.UserAgent = request.UserAgent;

            return entry;
        }

        static readonly object _processLocker = new object();
        private ThrottleCounter ProcessRequest(RequestIdentity requestIdentity, TimeSpan timeSpan, RateLimitPeriod period, out string id)
        {
            var throttleCounter = new ThrottleCounter()
            {
                Timestamp = DateTime.UtcNow,
                TotalRequests = 1
            };

            id = ComputeThrottleKey(requestIdentity, period);

            //serial reads and writes
            lock (_processLocker)
            {
                var entry = Repository.FirstOrDefault(id);
                if (entry.HasValue)
                {
                    //entry has not expired
                    if (entry.Value.Timestamp + timeSpan >= DateTime.UtcNow)
                    {
                        //increment request count
                        var totalRequests = entry.Value.TotalRequests + 1;

                        //deep copy
                        throttleCounter = new ThrottleCounter
                        {
                            Timestamp = entry.Value.Timestamp,
                            TotalRequests = totalRequests
                        };

                    }
                }

                //stores: id (string) - timestamp (datetime) - total (long)
                Repository.Save(id, throttleCounter, timeSpan);
            }

            return throttleCounter;
        }

        protected virtual string ComputeThrottleKey(RequestIdentity requestIdentity, RateLimitPeriod period)
        {
            var keyValues = new List<string>()
                {
                    "throttle"
                };

            if (Policy.IpThrottling)
                keyValues.Add(requestIdentity.ClientIp);

            if (Policy.ClientThrottling)
                keyValues.Add(requestIdentity.ClientKey);

            if (Policy.EndpointThrottling)
                keyValues.Add(requestIdentity.Endpoint);

            if (Policy.UserAgentThrottling)
                keyValues.Add(requestIdentity.UserAgent);

            keyValues.Add(period.ToString());

            var id = string.Join("_", keyValues);
            var idBytes = Encoding.UTF8.GetBytes(id);
            var hashBytes = new System.Security.Cryptography.SHA1Managed().ComputeHash(idBytes);
            var hex = BitConverter.ToString(hashBytes).Replace("-", "");
            return hex;
        }

        private string RetryAfterFrom(DateTime timestamp, RateLimitPeriod period)
        {
            var secondsPast = Convert.ToInt32((DateTime.UtcNow - timestamp).TotalSeconds);
            var retryAfter = 1;
            switch (period)
            {
                case RateLimitPeriod.Minute:
                    retryAfter = 60;
                    break;
                case RateLimitPeriod.Hour:
                    retryAfter = 60 * 60;
                    break;
                case RateLimitPeriod.Day:
                    retryAfter = 60 * 60 * 24;
                    break;
                case RateLimitPeriod.Week:
                    retryAfter = 60 * 60 * 24 * 7;
                    break;
            }
            retryAfter = retryAfter > 1 ? retryAfter - secondsPast : 1;
            return retryAfter.ToString(CultureInfo.InvariantCulture);
        }

        private bool IsWhitelisted(RequestIdentity requestIdentity)
        {
            if (Policy.IpThrottling)
                if (Policy.IpWhitelist != null && IpAddressParser.ContainsIp(Policy.IpWhitelist, requestIdentity.ClientIp))
                    return true;

            if (Policy.ClientThrottling)
                if (Policy.ClientWhitelist != null && Policy.ClientWhitelist.Contains(requestIdentity.ClientKey))
                    return true;

            if (Policy.EndpointThrottling)
                if (Policy.EndpointWhitelist != null && 
                    Policy.EndpointWhitelist.Any(x => requestIdentity.Endpoint.IndexOf(x, 0, StringComparison.InvariantCultureIgnoreCase) != -1))
                    return true;

            if (Policy.UserAgentThrottling && requestIdentity.UserAgent != null)
                if (Policy.UserAgentWhitelist != null && 
                    Policy.UserAgentWhitelist.Any(x => requestIdentity.UserAgent.IndexOf(x, 0, StringComparison.InvariantCultureIgnoreCase) != -1))
                    return true;

            return false;
        }

        private bool ApplyThrottling(ActionExecutingContext filterContext, out EnableThrottlingAttribute attr)
        {
            var applyThrottling = false;
            attr = null;

            if (filterContext.ActionDescriptor.ControllerDescriptor.IsDefined(typeof(EnableThrottlingAttribute), true))
            {
                attr = (EnableThrottlingAttribute)filterContext.ActionDescriptor.ControllerDescriptor.GetCustomAttributes(typeof(EnableThrottlingAttribute), true).First();
                applyThrottling = true;
            }
            
            //disabled on the class
            if (filterContext.ActionDescriptor.ControllerDescriptor.IsDefined(typeof(DisableThrottlingAttribute), true))
            {
                applyThrottling = false;
            }

            if (filterContext.ActionDescriptor.IsDefined(typeof(EnableThrottlingAttribute), true))
            {
                attr = (EnableThrottlingAttribute)filterContext.ActionDescriptor.GetCustomAttributes(typeof(EnableThrottlingAttribute), true).First();
                applyThrottling = true;
            }

            //explicit disabled
            if (filterContext.ActionDescriptor.IsDefined(typeof(DisableThrottlingAttribute), true))
            {
                applyThrottling = false;
            }

            return applyThrottling;
        }

        protected virtual ActionResult QuotaExceededResult(RequestContext filterContext, string message, HttpStatusCode responseCode, string requestId)
        {
            return new HttpStatusCodeResult(responseCode, message);
        }

        private ThrottleLogEntry ComputeLogEntry(string requestId, RequestIdentity identity, ThrottleCounter throttleCounter, string rateLimitPeriod, long rateLimit, HttpRequestBase request)
        {
            return new ThrottleLogEntry
            {
                ClientIp = identity.ClientIp,
                ClientKey = identity.ClientKey,
                Endpoint = identity.Endpoint,
                UserAgent = identity.UserAgent,
                LogDate = DateTime.UtcNow,
                RateLimit = rateLimit,
                RateLimitPeriod = rateLimitPeriod,
                RequestId = requestId,
                StartPeriod = throttleCounter.Timestamp,
                TotalRequests = throttleCounter.TotalRequests,
                Request = request
            };
        }
    }
}
