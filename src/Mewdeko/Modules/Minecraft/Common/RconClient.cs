using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Mewdeko.Modules.Minecraft.Common;

/// <summary>
///     A lightweight Minecraft RCON (Remote Console) client implementing the Source RCON protocol.
/// </summary>
public class RconClient : IDisposable
{
    private const int PacketTypeLogin = 3;
    private const int PacketTypeCommand = 2;
    private const int PacketTypeResponse = 0;
    private const int MaxResponseSize = 4096;
    private readonly NetworkStream stream;

    private readonly TcpClient tcpClient;
    private bool isDisposed;
    private int requestId;

    private RconClient(TcpClient tcpClient, NetworkStream stream)
    {
        this.tcpClient = tcpClient;
        this.stream = stream;
    }

    /// <summary>
    ///     Disposes the RCON connection.
    /// </summary>
    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;
        stream.Dispose();
        tcpClient.Dispose();
    }

    /// <summary>
    ///     Connects to an RCON server and authenticates.
    /// </summary>
    /// <param name="address">The server address.</param>
    /// <param name="port">The RCON port.</param>
    /// <param name="password">The RCON password.</param>
    /// <param name="timeout">Connection timeout in milliseconds.</param>
    /// <returns>An authenticated RCON client.</returns>
    public static async Task<RconClient> ConnectAsync(string address, int port, string password, int timeout = 5000)
    {
        var tcp = new TcpClient
        {
            ReceiveTimeout = timeout, SendTimeout = timeout
        };

        await tcp.ConnectAsync(address, port).WaitAsync(TimeSpan.FromMilliseconds(timeout));
        var networkStream = tcp.GetStream();
        var client = new RconClient(tcp, networkStream);

        var authResponse = await client.SendPacketAsync(PacketTypeLogin, password);
        if (authResponse.Id == -1)
        {
            client.Dispose();
            throw new InvalidOperationException("RCON authentication failed. Check the password.");
        }

        return client;
    }

    /// <summary>
    ///     Sends a command to the RCON server and returns the response.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <returns>The server's response text.</returns>
    public async Task<string> SendCommandAsync(string command)
    {
        var response = await SendPacketAsync(PacketTypeCommand, command);
        return response.Body;
    }

    private async Task<RconPacket> SendPacketAsync(int type, string body)
    {
        var id = Interlocked.Increment(ref requestId);
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var packetSize = 4 + 4 + bodyBytes.Length + 2;

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(packetSize);
        writer.Write(id);
        writer.Write(type);
        writer.Write(bodyBytes);
        writer.Write((short)0);

        var packet = ms.ToArray();
        await stream.WriteAsync(packet);

        return await ReadPacketAsync();
    }

    private async Task<RconPacket> ReadPacketAsync()
    {
        var sizeBuffer = new byte[4];
        await stream.ReadExactlyAsync(sizeBuffer);
        var size = BitConverter.ToInt32(sizeBuffer, 0);

        if (size > MaxResponseSize)
            size = MaxResponseSize;

        var bodyBuffer = new byte[size];
        await stream.ReadExactlyAsync(bodyBuffer);

        var responseId = BitConverter.ToInt32(bodyBuffer, 0);
        var responseType = BitConverter.ToInt32(bodyBuffer, 4);
        var responseBody = Encoding.UTF8.GetString(bodyBuffer, 8, size - 10);

        return new RconPacket(responseId, responseType, responseBody);
    }

    private record RconPacket(int Id, int Type, string Body);
}