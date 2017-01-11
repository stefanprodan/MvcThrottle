using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace MvcThrottle
{
    public class IpAddressParser : IIpAddressParser
    {
        public bool ContainsIp(List<string> ipRules, string clientIp)
        {
            var ip = ParseIp(clientIp);
            if (ipRules != null && ipRules.Any())
            {
                foreach (var rule in ipRules)
                {
                    var range = new IPAddressRange(rule);
                    if (range.Contains(ip))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool ContainsIp(List<string> ipRules, string clientIp, out string rule)
        {
            rule = null;
            var ip = ParseIp(clientIp);
            if (ipRules != null && ipRules.Any())
            {
                foreach (var r in ipRules)
                {
                    var range = new IPAddressRange(r);
                    if (range.Contains(ip))
                    {
                        rule = r;
                        return true;
                    }
                }
            }

            return false;
        }

        public virtual string GetClientIp(HttpRequestBase request)
        {
            var ipAddress = request.UserHostAddress;

            // get client IP from reverse proxy
            var xForwardedFor = request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            if (!string.IsNullOrEmpty(xForwardedFor))
            {
                // Search for public IP addresses 
                var publicForwardingIps = xForwardedFor.Split(',').Where(ip => !IsPrivateIpAddress(ip)).ToList();

                // If we found any public IP, return the last one, otherwise return the user host address
                return publicForwardingIps.Any() ? publicForwardingIps.Last().Trim() : ipAddress;
            }

            return ipAddress;
        }

        /// <summary>
        /// Parse IP by stripping port value if any (fix for Azure LB)
        /// </summary>
        public IPAddress ParseIp(string ipAddress)
        {
            ipAddress = ipAddress.Trim();
            int portDelimiterPos = ipAddress.LastIndexOf(":", StringComparison.InvariantCultureIgnoreCase);
            bool ipv6WithPortStart = ipAddress.StartsWith("[");
            int ipv6End = ipAddress.IndexOf("]");
            if (portDelimiterPos != -1
                && portDelimiterPos == ipAddress.IndexOf(":", StringComparison.InvariantCultureIgnoreCase)
                || ipv6WithPortStart && ipv6End != -1 && ipv6End < portDelimiterPos)
            {
                ipAddress = ipAddress.Substring(0, portDelimiterPos);
            }

            return IPAddress.Parse(ipAddress);
        }

        public bool IsPrivateIpAddress(string ipAddress)
        {
            // http://en.wikipedia.org/wiki/Private_network
            // Private IP Addresses are: 
            //  24-bit block: 10.0.0.0 through 10.255.255.255
            //  20-bit block: 172.16.0.0 through 172.31.255.255
            //  16-bit block: 192.168.0.0 through 192.168.255.255
            //  Link-local addresses: 169.254.0.0 through 169.254.255.255 (http://en.wikipedia.org/wiki/Link-local_address)

            var ip = ParseIp(ipAddress);
            var octets = ip.GetAddressBytes();

            bool isIpv6 = octets.Length == 16;

            if (isIpv6)
            {
                bool isUniqueLocalAddress = octets[0] == 253;
                return isUniqueLocalAddress;
            }
            else
            {
                var is24BitBlock = octets[0] == 10;
                if (is24BitBlock) return true; 

                var is20BitBlock = octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31;
                if (is20BitBlock) return true;

                var is16BitBlock = octets[0] == 192 && octets[1] == 168;
                if (is16BitBlock) return true; 

                var isLinkLocalAddress = octets[0] == 169 && octets[1] == 254;
                return isLinkLocalAddress;
            }
        }

    }
}
