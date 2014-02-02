using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace MvcThrottle
{
    public class EnableThrottlingAttribute : ActionFilterAttribute, IActionFilter
    {

    }


    public class DisableThrottingAttribute : ActionFilterAttribute, IActionFilter
    {

    }
}
