using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Mewdeko.Common.Attributes.TextCommands;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    public class NetworkUtilities : MewdekoSubmodule
    {
        [Cmd, Aliases, Ratelimit(10)]
        public async Task PingIp(string ip, int count = 1)
        {
            var typing = ctx.Channel.EnterTypingState();
            var ping = new Ping();
            var embeds = new List<Embed>();
            if (count > 10)
            {
                await ctx.Channel.SendErrorAsync("Maximum of 10 pings.");
                typing.Dispose();
                return;
            }
            for (var i = 0; i < count; i++)
            {
                var pingReply = await ping.SendPingAsync(ip);
                if (pingReply.Status == IPStatus.Unknown)
                {
                    await ctx.Channel.SendErrorAsync("The ICMP Echo failed for an unknown reason, cannot continue.");
                    typing.Dispose();
                    break;
                }

                if (pingReply.Status == IPStatus.Success)
                {
                    var eb = new EmbedBuilder()
                        .WithTitle($"Ping #{i + 1}")
                        .WithDescription($"Address: {pingReply.Address}\nLatency: {pingReply.RoundtripTime}ms")
                        .WithOkColor();
                    embeds.Add(eb.Build());
                }
                else if (pingReply.Status == IPStatus.DestinationNetworkUnreachable)
                {
                    await ctx.Channel.SendErrorAsync("The destination network is unreachable, cannot continue.");
                    typing.Dispose();
                    break;
                }
                else if (pingReply.Status == IPStatus.DestinationHostUnreachable)
                {
                    await ctx.Channel.SendErrorAsync("The destination host is unreachable, cannot continue.");
                    typing.Dispose();
                    break;
                }
                else if (pingReply.Status == IPStatus.DestinationPortUnreachable)
                {
                    await ctx.Channel.SendErrorAsync("The destination port is unreachable, cannot continue.");
                    typing.Dispose();
                    break;
                }
                else if (pingReply.Status == IPStatus.NoResources)
                {
                    await ctx.Channel.SendErrorAsync("There is insufficent network resources to complete this request. (How the fuck did you stumble on this???)");
                    typing.Dispose();
                    break;
                }
                else if (pingReply.Status == IPStatus.BadOption)
                {
                    await ctx.Channel.SendErrorAsync("Ping failed due to a bad option. (Again, how the fuck did you stumble on this???)");
                    typing.Dispose();
                    break;
                }
                else if (pingReply.Status == IPStatus.HardwareError)
                {
                    await ctx.Channel.SendErrorAsync("Ping failed due to a hardware error. Please report this at https://discord.gg/mewdeko.");
                    typing.Dispose();
                    break;
                }
                else if (pingReply.Status == IPStatus.TimedOut)
                {
                    await ctx.Channel.SendErrorAsync("Timed out attempting to recieve a ping cannot continue.");
                    typing.Dispose();
                    break;
                }
                else if (pingReply.Status == IPStatus.BadDestination)
                {
                    await ctx.Channel.SendErrorAsync("Double check that you entered a correct IP and try again.");
                    typing.Dispose();
                    break;
                }
                else if (pingReply.Status == IPStatus.DestinationUnreachable)
                {
                    await ctx.Channel.SendErrorAsync("The destination is unreachable, cannot continue.");
                    typing.Dispose();
                    break;
                }
            }

            if (embeds.Any())
                await ctx.Channel.SendMessageAsync(embeds: embeds.ToArray());
            typing.Dispose();
        }
    }

    [Cmd, Aliases, Ratelimit(10)]
    public async Task Traceroute(string hostname)
    {
        var toDispose = ctx.Channel.EnterTypingState();
        var traceRt = GetTraceRoute(hostname);
        if (traceRt.Any())
        {
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithDescription(string.Join("\n", traceRt.Select(x => $"{x}")));
            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }
        else
            await ctx.Channel.SendErrorAsync("Seems like traceroute was not successful. Please double check the hostname/ip and try again.");
        toDispose.Dispose();
    }

    public static IEnumerable<IPAddress> GetTraceRoute(string hostname)
    {
        // following are similar to the defaults in the "traceroute" unix command.
        const int timeout = 10000;
        const int maxTtl = 30;
        const int bufferSize = 32;

        var buffer = new byte[bufferSize];
        new Random().NextBytes(buffer);

        using (var pinger = new Ping())
        {
            for (var ttl = 1; ttl <= maxTtl; ttl++)
            {
                var options = new PingOptions(ttl, true);
                var reply = pinger.Send(hostname, timeout, buffer, options);

                // we've found a route at this ttl
                if (reply.Status == IPStatus.Success || reply.Status == IPStatus.TtlExpired)
                    yield return reply.Address;

                // if we reach a status other than expired or timed out, we're done searching or there has been an error
                if (reply.Status != IPStatus.TtlExpired && reply.Status != IPStatus.TimedOut)
                    break;
            }
        }
    }
}