using System;
using System.Linq;
using System.Net;
using System.Threading;
using ipdb;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Responses;

namespace Arashi
{
    public class GeoIP
    {
        private static string SetupBasePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;

        public static string GetCnISP(AsnResponse asnResponse)
        {
            try
            {
                var asName = asnResponse.AutonomousSystemOrganization.ToLower();

                if (asName.Contains("cernet") || asName.Contains("education") || asName.Contains("research") ||
                    asName.Contains("university") || asName.Contains("academy") ||
                    asName.Contains("computer network information center"))
                    return "CERNET";
                if (asName.Contains("mobile") || asName.Contains("cmnet") || asName.Contains("tietong") ||
                    asName.Contains("railway"))
                    return "CM";
                if (asName.Contains("unicom") || asName.Contains("cnc") ||
                    asName.Contains("china169") || asName.Contains("netcom"))
                    return "CU";
                if (asName.Contains("chinanet") || asName.Contains("telecom") || asName.Contains("no.31,jin-rong") ||
                    asName.Contains("inter-exchange") || asName.Contains("ct"))
                    return "CT";

                return string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public static string GetGeoStr(IPAddress ipAddress)
        {
            try
            {
                var isZH = Thread.CurrentThread.CurrentCulture.Name.Contains("zh");
                if (IPAddress.IsLoopback(ipAddress) || Equals(ipAddress, IPAddress.Any))
                    return string.Empty;

                var asStr = string.Empty;
                var asnResponse = new DatabaseReader(SetupBasePath + "GeoLite2-ASN.mmdb").Asn(ipAddress);
                asStr += $"[{asnResponse.AutonomousSystemOrganization} / AS{asnResponse.AutonomousSystemNumber}] "
                    .PadRight(50);

                var cityStr = string.Empty;
                if (isZH)
                    cityStr += string.Join(" ",
                            new District(SetupBasePath + "ipipfree.ipdb").find(ipAddress.ToString(), "CN").Distinct())
                        .PadRight(8);
                else
                {
                    var cityResponse = new DatabaseReader(SetupBasePath + "GeoLite2-City.mmdb").City(ipAddress);
                    if (!string.IsNullOrWhiteSpace(cityResponse.Country.IsoCode))
                        cityStr += cityResponse.Country.IsoCode + " ";
                    if (!string.IsNullOrWhiteSpace(cityResponse.MostSpecificSubdivision.IsoCode))
                        cityStr += cityResponse.MostSpecificSubdivision.IsoCode + " ";
                    if (!string.IsNullOrWhiteSpace(cityResponse.City.Name))
                        cityStr += cityResponse.City.Name + " ";
                    cityStr = cityStr.PadRight(20);
                }

                //if (isZH&& cityStr.Contains("中国"))
                //{
                //    var cnIsp = GetCnISP(asnResponse);
                //    asStr += !string.IsNullOrEmpty(cnIsp) ? $"[{cnIsp}] ".PadLeft(5) : "     ";
                //}

                return asStr + cityStr;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
