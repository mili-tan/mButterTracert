using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Arashi;
using McMaster.Extensions.CommandLineUtils;

namespace ButterTracert
{
    class Program
    {
        static void Main(string[] args)
        {
            var isZh = Thread.CurrentThread.CurrentCulture.Name.Contains("zh");
            var cmd = new CommandLineApplication
            {
                Name = "mButterTracert",
                Description = "mButterTracert - Simple parallelism based fast traceroute tool." +
                              Environment.NewLine +
                              $"Copyright (c) {DateTime.Now.Year} Milkey Tan. Code released under the MIT License" +
                              Environment.NewLine + Environment.NewLine +
                              "This product includes GeoLite2 data created by MaxMind, available from https://www.maxmind.com" +
                              (isZh
                                  ? Environment.NewLine +
                                    "This product includes FreeIPDB data created by IPIP.net, available from https://en.ipip.net"
                                  : string.Empty)
            };
            cmd.HelpOption("-?|--help");

            var hostArg = cmd.Argument("host", isZh ? "指定的目标主机地址。" : "Target host address");
            var hOption = cmd.Option<int>("-h <maximum_hops>",
                isZh ? "追踪至目标主机的最大跃点数。" : "Maximum number of hops for tracking to the target host", CommandOptionType.SingleValue);
            var wOption = cmd.Option<int>("-w <timeout>",
                isZh ? "等待每次回复的超时时间(毫秒)。" : "Timeout time to wait for each reply", CommandOptionType.SingleValue);
            var cOption = cmd.Option<int>("-c <count>",
                isZh ? "重试发送的回显请求数。" : "Number of echo requests to retry sending", CommandOptionType.SingleValue);
            var rOption = cmd.Option<int>("-r",
                isZh ? "当跃点超时重试发送请求。" : "Retry sending request when hops timeout", CommandOptionType.NoValue);
            var lOption = cmd.Option<int>("-l",
                isZh ? "列出各跃点的往返延迟。" : "List the round-trip latency for each hops", CommandOptionType.NoValue);
            var nOption = cmd.Option<int>("-n",
                isZh ? "不显示 IP 地理位置信息与 ASN。" : "Do not show IP geolocation and ASN information", CommandOptionType.NoValue);

            cmd.OnExecute(() =>
            {
                var hops = hOption.HasValue() ? hOption.ParsedValue : 30;
                var wait = wOption.HasValue() ? wOption.ParsedValue : 1000;
                var rcount = cOption.HasValue() ? cOption.ParsedValue : 3;

                if (!nOption.HasValue()) DownloadIPDB();
                if (string.IsNullOrWhiteSpace(hostArg.Value))
                {
                    Console.WriteLine((isZh ? "指定的目标主机地址不应该为空。" : "The target host address should not be empty.") +
                                      Environment.NewLine);
                    cmd.ShowHelp();
                    return;
                }

                Console.WriteLine(Environment.NewLine + (isZh
                    ? $"通过最大 {hops} 个跃点跟踪至 {hostArg.Value} 的路由"
                    : $"Trace routes to {hostArg.Value} via Maximum of {hops} hops.") + Environment.NewLine);
                
                var latency = new Dictionary<IPAddress, int>();
                var ips = TraceRoute(hostArg.Value, wait, hops, rOption.HasValue(), rcount);
                if (lOption.HasValue())
                {
                    Parallel.ForEach(ips, i =>
                    {
                        try
                        {
                            latency.TryAdd(i.Value, Convert.ToInt32(new Ping().Send(i.Value, 1000).RoundtripTime));
                        }
                        catch (Exception)
                        {
                            latency.TryAdd(i.Value, 0);
                        }
                    });
                    Thread.Sleep(100);
                }

                foreach (var item in ips)
                    try
                    {
                        Console.WriteLine(
                            item.Key.ToString().PadRight(3, ' ') + " " +
                            item.Value.ToString().PadLeft(15, ' ') + " " +
                            (lOption.HasValue() && latency.ContainsKey(item.Value)
                                ? latency[item.Value] + "ms"
                                : string.Empty).PadLeft(5, ' ') + " " +
                            (nOption.HasValue() ? string.Empty : GeoIP.GetGeoStr(item.Value)));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                Console.WriteLine(Environment.NewLine + (isZh ? "追踪完成。" : "Tracing completed") + Environment.NewLine);
            });

            cmd.Execute(args);
        }

        public static string GetHostName(IPAddress ip)
        {
            try
            {
                return Dns.GetHostEntry(ip).HostName;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public static void ClearCurrentConsoleLine()
        {
            Console.SetCursorPosition(0, Console.CursorTop - 1);
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLineCursor);
        }

        public static Dictionary<int, IPAddress> TraceRoute(string hostname, int timeout, int maxTTL,
            bool retry = false, int retryCount = 3)
        {
            var bufferSize = 32;
            var dict = new Dictionary<int, IPAddress>();
            var buffer = new byte[bufferSize];
            new Random().NextBytes(buffer);
            Parallel.For(1, maxTTL, ttl =>
            {
                var reply = new Ping().Send(hostname, timeout, buffer, new PingOptions(ttl, true));
                if (retry)
                    for (var i = 0; i < retryCount; i++)
                        if (reply.Status == IPStatus.TimedOut)
                            reply = new Ping().Send(hostname, timeout / 2, buffer, new PingOptions(ttl, true));
                        else
                            break;


                if (!dict.ContainsValue(reply.Address))
                {
                    switch (reply.Status)
                    {

                        case IPStatus.TtlExpired:
                        case IPStatus.Success:
                            dict.Add(ttl, reply.Address);
                            break;
                        case IPStatus.TimedOut:
                            dict.Add(ttl, IPAddress.Any);
                            break;
                    }
                }

            });

            dict = dict.OrderBy(o => o.Key).ToDictionary(o => o.Key, p => p.Value);
            var last = dict.Last();
            dict.Remove(last.Key);
            dict.Add(dict.Last().Key + 1, last.Value);
            return dict;
        }
        public static void DownloadIPDB()
        {
            var basePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            var isZh = Thread.CurrentThread.CurrentCulture.Name.Contains("zh");
            if ((isZh
                ? !File.Exists(basePath + "ipipfree.ipdb")
                : !File.Exists(basePath + "GeoLite2-City.mmdb")) || !File.Exists(basePath + "GeoLite2-ASN.mmdb"))
            {
                Task.WaitAny(
                    Task.Run(() =>
                    {
                        var tasks = new List<Task>();
                        tasks.Add(new WebClient().DownloadFileTaskAsync(
                            isZh
                                ? "https://mili-01.coding.net/p/k1/d/maxmind-geoip/git/raw/release/GeoLite2-ASN.mmdb"
                                : "https://github.com/mili-tan/maxmind-geoip/raw/release/GeoLite2-ASN.mmdb",
                            basePath + "GeoLite2-ASN.mmdb"));
                        if (isZh)
                        {
                            new WebClient().DownloadFileTaskAsync(
                                "https://mili-01.coding.net/p/k1/d/ipipdb/git/raw/master/ipipfree.ipdb",
                                basePath + "ipipfree.ipdb");
                        }
                        else
                        {
                            new WebClient().DownloadFileTaskAsync(
                                "https://github.com/mili-tan/maxmind-geoip/raw/release/GeoLite2-City.mmdb",
                                basePath + "GeoLite2-City.mmdb");
                        }

                        Task.WaitAll(tasks.ToArray());
                    }),
                    Task.Run(() =>
                    {
                        Console.WriteLine(
                            "This product includes GeoLite2 data created by MaxMind, available from https://www.maxmind.com");
                        if (isZh)
                            Console.WriteLine(
                                "This product includes FreeIPDB data created by IPIP.net, available from https://en.ipip.net");
                        while (true)
                        {
                            Console.WriteLine("Downloading GeoIP Database  |");
                            ClearCurrentConsoleLine();
                            Thread.Sleep(100);
                            Console.WriteLine("Downloading GeoIP Database  /");
                            Thread.Sleep(100);
                            ClearCurrentConsoleLine();
                            Console.WriteLine("Downloading GeoIP Database  -");
                            Thread.Sleep(100);
                            ClearCurrentConsoleLine();
                            Console.WriteLine("Downloading GeoIP Database  \\");
                            Thread.Sleep(100);
                            ClearCurrentConsoleLine();
                        }
                    }));
            }
        }
    }
}
