using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace ButterTracert
{
    class Program
    {
        static void Main(string[] args)
        {
            var cmd = new CommandLineApplication
            {
                Name = "mButterTracert",
                Description = "mButterTracert - Simple parallelism based fast traceroute tool." +
                              Environment.NewLine +
                              $"Copyright (c) {DateTime.Now.Year} Milkey Tan. Code released under the MIT License"
            };
            cmd.HelpOption("-?|--help");
            
            var isZh = Thread.CurrentThread.CurrentCulture.Name.Contains("zh");
            var hostArg = cmd.Argument("host", isZh ? "指定的目标主机地址。" : "Target host address");
            var hOption = cmd.Option<int>("-h <maximum_hops>",
                isZh ? "追踪至目标主机的最大跃点数。" : "Maximum number of hops for tracking to the target host", CommandOptionType.SingleValue);
            var wOption = cmd.Option<int>("-w <timeout>",
                isZh ? "等待每次回复的超时时间(毫秒)。" : "Timeout time to wait for each reply", CommandOptionType.SingleValue);
            var cOption = cmd.Option<int>("-c <count>",
                isZh ? "重试发送的回显请求数。" : "Number of echo requests to retry sending", CommandOptionType.SingleValue);
            var rOption = cmd.Option<int>("-r",
                isZh ? "当跃点超时重试发送请求。" : "Retry sending request when hops timeout", CommandOptionType.NoValue);

            cmd.OnExecute(() =>
            {
                var hops = hOption.HasValue() ? hOption.ParsedValue : 30;
                var wait = wOption.HasValue() ? wOption.ParsedValue : 1000;
                var rcount = cOption.HasValue() ? cOption.ParsedValue : 3;

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
                var ips = TraceRoute(hostArg.Value, wait, hops, rOption.HasValue(), rcount);
                foreach (var item in ips) Console.WriteLine(item.Key + " " + item.Value);
                Console.WriteLine(Environment.NewLine + (isZh ? "追踪完成。" : "Tracing completed") + Environment.NewLine);
            });

            cmd.Execute(args);
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
            var last = dict.Last();
            dict.Remove(last.Key);
            dict.Add(dict.Last().Key + 1, last.Value);
            return dict;
        }
    }
}
