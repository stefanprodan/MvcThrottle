using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace MvcThrottle.Demo.Helpers
{
    public class MvcThrottleCustomFilter : MvcThrottle.ThrottlingFilter
    {
        protected override ActionResult QuotaExceededResult(RequestContext context, string message, System.Net.HttpStatusCode responseCode)
        {
            var rateLimitedView = new ViewResult
            {
                ViewName = "RateLimited"
            };
            rateLimitedView.ViewData["Message"] = message;

            return rateLimitedView;
        }
    }
}