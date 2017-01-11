using System.Collections.Generic;
using System.Net;
using System.Web;

namespace MvcThrottle
{
    public interface IIpAddressParser
    {
        bool ContainsIp(List<string> ipRules, string clientIp);
        bool ContainsIp(List<string> ipRules, string clientIp, out string rule);
        string GetClientIp(HttpRequestBase request);
        bool IsPrivateIpAddress(string ipAddress);
        IPAddress ParseIp(string ipAddress);
    }
}