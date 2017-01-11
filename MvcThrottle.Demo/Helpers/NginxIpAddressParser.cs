using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MvcThrottle.Demo.Helpers
{
    public class NginxIpAddressParser : IpAddressParser
    {
        public override string GetClientIp(HttpRequestBase request)
        {
            var ipAddress = request.UserHostAddress;

            // get client IP from reverse proxy
            var xForwardedFor = request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            if (!string.IsNullOrEmpty(xForwardedFor))
            {
                // Search for public IP addresses 
                var publicForwardingIps = xForwardedFor.Split(',').Where(ip => !IsPrivateIpAddress(ip)).ToList();

                // If we found any public IP, return the first one when NGNIX is used, otherwise return the user host address
                return publicForwardingIps.Any() ? publicForwardingIps.First().Trim() : ipAddress;
            }

            return ipAddress;
        }
    }
}