using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace MvcThrottle.Demo.Controllers
{
    [EnableThrottling(PerSecond = 1 , PerMinute = 12)]
    public class HomeController : Controller
    {
        [EnableThrottling(PerSecond = 2, PerMinute = 5)]
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}