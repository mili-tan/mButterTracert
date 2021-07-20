using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace ButterTracert
{
    class Program
    {
        static void Main(string[] args)
        {
            var ips = TraceRoute("8.8.8.8");
            foreach (var item in ips) Console.WriteLine(item.Key + ":" + item.Value);
        }

        public static Dictionary<int, IPAddress> TraceRoute(string hostname, bool retry = false)
        {
            var timeout = 1000;
            var maxTTL = 30;
            var bufferSize = 32;

            var dict = new Dictionary<int, IPAddress>();
            var buffer = new byte[bufferSize];
            new Random().NextBytes(buffer);
            Parallel.For(1, maxTTL, ttl =>
            {
                var reply = new Ping().Send(hostname, timeout, buffer, new PingOptions(ttl, true));
                if (retry)
                    for (var i = 0; i < 3; i++)
                        if (reply.Status == IPStatus.TimedOut)
                            reply = new Ping().Send(hostname, timeout / 3, buffer, new PingOptions(ttl, true));
                        else
                            break;

                switch (reply.Status)
                {
                    case IPStatus.TtlExpired:
                    case IPStatus.Success when !dict.ContainsValue(reply.Address):
                        dict.Add(ttl, reply.Address);
                        break;
                    case IPStatus.TimedOut:
                        dict.Add(ttl, IPAddress.Any);
                        break;
                    case IPStatus.Success:
                        break;
                }
            });

            dict = dict.OrderBy(o => o.Key).ToDictionary(o => o.Key, p => p.Value);
            if (dict.ContainsKey(dict.ElementAt(dict.Count - 2).Key + 1)) return dict;
            dict.Add(dict.ElementAt(dict.Count - 2).Key + 1, dict.Last().Value);
            dict.Remove(dict.ElementAt(dict.Count - 2).Key);
            return dict;
        }
    }
}
