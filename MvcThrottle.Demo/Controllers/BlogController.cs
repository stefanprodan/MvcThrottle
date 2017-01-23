using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace MvcThrottle.Demo.Controllers
{
    [DisableThrottling]
    public class BlogController : BaseController
    {
        public ActionResult Index()
        {
            ViewBag.Message = "The blog is not throttled.";

            return View();
        }

        [EnableThrottling(PerSecond = 2, PerMinute = 5)]
        public ActionResult Search()
        {
            ViewBag.Message = "Searches are throttled.";

            return View();
        }
    }
}
