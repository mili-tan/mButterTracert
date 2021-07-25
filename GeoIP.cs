using System;
using System.Net;
using System.Threading.Tasks;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Responses;

namespace Arashi
{
    public class GeoIP
    {
        private static string SetupBasePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
        public static DatabaseReader AsnReader = new(SetupBasePath + "GeoLite2-ASN.mmdb");
        public static DatabaseReader CityReader = new(SetupBasePath + "GeoLite2-City.mmdb");
        public static AsnResponse GetAsnResponse(IPAddress ipAddress) => AsnReader.Asn(ipAddress);
        public static CityResponse GetCityResponse(IPAddress ipAddress) => CityReader.City(ipAddress);

        public static (AsnResponse, CityResponse) GetAsnCityValueTuple(IPAddress ipAddress)
        {
            var asn = new AsnResponse();
            var city = new CityResponse();
            Task.WaitAll(
                Task.Run(() => asn = GetAsnResponse(ipAddress)),
                Task.Run(() => city = GetCityResponse(ipAddress)));
            return (asn, city);
        }

        public static string GetCnISP(AsnResponse asnResponse, CityResponse cityResponse)
        {
            try
            {
                var country = cityResponse.Country.IsoCode;
                var asName = asnResponse.AutonomousSystemOrganization.ToLower();

                if (country != "CN") return string.Empty;

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
                if (IPAddress.IsLoopback(ipAddress) || Equals(ipAddress, IPAddress.Any))
                    return string.Empty;
                var (asnResponse, cityResponse) = GetAsnCityValueTuple(ipAddress);
                var cnIsp = GetCnISP(asnResponse, cityResponse);

                var asStr = string.Empty;

                try
                {
                    asStr += $"[{asnResponse.AutonomousSystemOrganization} / AS{asnResponse.AutonomousSystemNumber}] "
                        .PadRight(50);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                var cityStr = string.Empty;
                if (!string.IsNullOrWhiteSpace(cityResponse.Country.IsoCode))
                    cityStr += cityResponse.Country.IsoCode + " ";
                if (!string.IsNullOrWhiteSpace(cityResponse.MostSpecificSubdivision.IsoCode))
                    cityStr += cityResponse.MostSpecificSubdivision.IsoCode + " ";
                if (!string.IsNullOrWhiteSpace(cityResponse.City.Name))
                    cityStr += cityResponse.City.Name + " ";

                cityStr = cityStr.PadRight(20);

                if (!string.IsNullOrEmpty(cnIsp))
                    cityStr += $"[{cnIsp}] ".PadLeft(10);

                return asStr + cityStr;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
