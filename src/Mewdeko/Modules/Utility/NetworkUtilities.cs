using System.Net;
using System.Net.NetworkInformation;
using Mewdeko.Common.Attributes.TextCommands;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    /// Commands for network utilities.
    /// </summary>
    public class NetworkUtilities : MewdekoSubmodule
    {
        /// <summary>
        /// Pings an IP address. Maximum of 10 pings.
        /// </summary>
        /// <param name="ip">The IP address to ping.</param>
        /// <param name="count">The number of pings to send. Default is 1.</param>
        /// <remarks>
        /// Has a ratelimit of 10 uses per 10 seconds.
        /// </remarks>
        [Cmd, Aliases, Ratelimit(10)]
        public async Task PingIp(string ip, int count = 1)
        {
            var typing = ctx.Channel.EnterTypingState();
            var ping = new Ping();
            var embeds = new List<Embed>();
            if (count > 10)
            {
                await ctx.Channel.SendErrorAsync("Maximum of 10 pings.", Config);
                typing.Dispose();
                return;
            }

            for (var i = 0; i < count; i++)
            {
                var pingReply = await ping.SendPingAsync(ip);
                if (pingReply.Status == IPStatus.Unknown)
                {
                    await ctx.Channel.SendErrorAsync("The ICMP Echo failed for an unknown reason, cannot continue.",
                        Config);
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
                    await ctx.Channel.SendErrorAsync("The destination network is unreachable, cannot continue.",
                        Config);
                    typing.Dispose();
                    break;
                }
                else if (pingReply.Status == IPStatus.DestinationHostUnreachable)
                {
                    await ctx.Channel.SendErrorAsync("The destination host is unreachable, cannot continue.", Config);
                    typing.Dispose();
                    break;
                }
                else if (pingReply.Status == IPStatus.DestinationPortUnreachable)
                {
                    await ctx.Channel.SendErrorAsync("The destination port is unreachable, cannot continue.", Config);
                    typing.Dispose();
                    break;
                }
                else if (pingReply.Status == IPStatus.NoResources)
                {
                    await ctx.Channel.SendErrorAsync(
                        "There is insufficent network resources to complete this request. (How the fuck did you stumble on this???)",
                        Config);
                    typing.Dispose();
                    break;
                }
                else if (pingReply.Status == IPStatus.BadOption)
                {
                    await ctx.Channel.SendErrorAsync(
                        "Ping failed due to a bad option. (Again, how the fuck did you stumble on this???)", Config);
                    typing.Dispose();
                    break;
                }
                else if (pingReply.Status == IPStatus.HardwareError)
                {
                    await ctx.Channel.SendErrorAsync(
                        "Ping failed due to a hardware error. Please report this at https://discord.gg/mewdeko.",
                        Config);
                    typing.Dispose();
                    break;
                }
                else if (pingReply.Status == IPStatus.TimedOut)
                {
                    await ctx.Channel.SendErrorAsync("Timed out attempting to recieve a ping cannot continue.", Config);
                    typing.Dispose();
                    break;
                }
                else if (pingReply.Status == IPStatus.BadDestination)
                {
                    await ctx.Channel.SendErrorAsync("Double check that you entered a correct IP and try again.",
                        Config);
                    typing.Dispose();
                    break;
                }
                else if (pingReply.Status == IPStatus.DestinationUnreachable)
                {
                    await ctx.Channel.SendErrorAsync("The destination is unreachable, cannot continue.", Config);
                    typing.Dispose();
                    break;
                }
            }

            if (embeds.Any())
                await ctx.Channel.SendMessageAsync(embeds: embeds.ToArray());
            typing.Dispose();
        }
    }

    /// <summary>
    /// Executes a traceroute operation to the specified hostname, displaying the route that packets take to reach an IP address or domain.
    /// </summary>
    /// <param name="hostname">The IP address or domain name to trace the route to.</param>
    /// <returns>A task that represents the asynchronous operation of sending the traceroute results as a message.</returns>
    /// <remarks>
    /// This command simulates the traceroute utility commonly found in Unix-based operating systems, tracing the path packets take to reach a network host.
    /// It's useful for diagnosing network issues by identifying points of failure or delay in the route to the target host.
    /// Due to the nature of ICMP protocol limitations and network configurations, some hops may not respond (e.g., due to firewalls),
    /// resulting in incomplete paths. The operation might take a while depending on the number of hops and network conditions, hence the bot will show a typing indicator during execution.
    /// </remarks>
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
            await ctx.Channel.SendErrorAsync(
                "Seems like traceroute was not successful. Please double check the hostname/ip and try again.", Config);

        toDispose.Dispose();
    }

    /// <summary>
    /// Generates the sequence of IP addresses representing the path taken to the specified hostname using ICMP echo requests.
    /// </summary>
    /// <param name="hostname">The target hostname or IP address for the traceroute operation.</param>
    /// <returns>An enumerable collection of IP addresses representing each hop in the route.</returns>
    /// <remarks>
    /// This method sends ICMP packets with incrementing Time-to-Live (TTL) values to trace the route to the specified target.
    /// Each hop along the route decrements the TTL by one, and when it reaches zero, it responds with an ICMP "Time Exceeded" message,
    /// allowing the traceroute operation to identify each router or hop along the path.
    /// If the operation reaches the target or encounters an error before the maximum TTL, it terminates early.
    /// </remarks>
    public static IEnumerable<IPAddress> GetTraceRoute(string hostname)
    {
        // following are similar to the defaults in the "traceroute" unix command.
        const int timeout = 10000;
        const int maxTtl = 30;
        const int bufferSize = 32;

        var buffer = new byte[bufferSize];
        new Random().NextBytes(buffer);

        using var pinger = new Ping();
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