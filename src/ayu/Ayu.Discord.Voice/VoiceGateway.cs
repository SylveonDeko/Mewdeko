using Ayu.Discord.Voice.Models;
using Discord.Models.Gateway;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Ayu.Discord.Gateway;
using Newtonsoft.Json;

namespace Ayu.Discord.Voice
{
    public class VoiceGateway
    {
        private class QueueItem
        {
            public VoicePayload Payload { get; }
            public TaskCompletionSource<bool> Result { get; }

            public QueueItem(VoicePayload payload, TaskCompletionSource<bool> result)
            {
                Payload = payload;
                Result = result;
            }
        }

        private readonly ulong _guildId;
        private readonly ulong _userId;
        private readonly string _sessionId;
        private readonly string _token;
        private readonly string _endpoint;
        private readonly Uri _websocketUrl;
        private readonly Channel<QueueItem> _channel;

        public TaskCompletionSource<bool> ConnectingFinished { get; }

        private readonly Random _rng;
        private readonly SocketClient _ws;
        private readonly UdpClient _udpClient;
        private Timer? _heartbeatTimer;
        private bool _receivedAck;
        private IPEndPoint? _udpEp;

        public uint Ssrc { get; private set; }
        public string Ip { get; private set; } = "";
        public int Port { get; private set; } = 0;
        public byte[] SecretKey { get; private set; } = Array.Empty<byte>();
        public string Mode { get; private set; } = "";
        public ushort Sequence { get; set; }
        public uint NonceSequence { get; set; }
        public uint Timestamp { get; set; }
        public string MyIp { get; private set; } = "";
        public ushort MyPort { get; private set; }
        private bool shouldResume = false;
        
        private readonly CancellationTokenSource _stopCancellationSource;
        private readonly CancellationToken _stopCancellationToken;
        public bool Stopped => _stopCancellationToken.IsCancellationRequested;

        public event Func<VoiceGateway, Task> OnClosed = delegate { return Task.CompletedTask; };

        public VoiceGateway(ulong guildId, ulong userId, string session, string token, string endpoint)
        {
            this._guildId = guildId;
            this._userId = userId;
            this._sessionId = session;
            this._token = token;
            this._endpoint = endpoint;

            //Log.Information("g: {GuildId} u: {UserId} sess: {Session} tok: {Token} ep: {Endpoint}",
            //    guildId, userId, session, token, endpoint);

            this._websocketUrl = new Uri($"wss://{_endpoint.Replace(":80", "")}?v=4");
            this._channel = Channel.CreateUnbounded<QueueItem>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            });

            ConnectingFinished = new TaskCompletionSource<bool>();

            _rng = new Random();

            _ws = new SocketClient();
            _udpClient = new UdpClient();
            _stopCancellationSource = new CancellationTokenSource();
            _stopCancellationToken = _stopCancellationSource.Token;

            _ws.PayloadReceived += _ws_PayloadReceived;
            _ws.WebsocketClosed += _ws_WebsocketClosed;
        }

        public Task WaitForReadyAsync()
            => ConnectingFinished.Task;

        private async Task SendLoop()
        {
            while (!_stopCancellationToken.IsCancellationRequested)
            {
                try
                {
                    var qi = await _channel.Reader.ReadAsync(_stopCancellationToken);
                    //Log.Information("Sending payload with opcode {OpCode}", qi.Payload.OpCode);

                    var json = JsonConvert.SerializeObject(qi.Payload);

                    if (!_stopCancellationToken.IsCancellationRequested)
                        await _ws.SendAsync(Encoding.UTF8.GetBytes(json));
                    _ = Task.Run(() => qi.Result.TrySetResult(true));
                }
                catch (ChannelClosedException)
                {
                    Log.Warning("Voice gateway send channel is closed");
                }
            }
        }

        private async Task _ws_PayloadReceived(byte[] arg)
        {
            var payload = JsonConvert.DeserializeObject<VoicePayload>(Encoding.UTF8.GetString(arg));
            try
            {
                //Log.Information("Received payload with opcode {OpCode}", payload.OpCode);

                switch (payload.OpCode)
                {
                    case VoiceOpCode.Identify:
                        // sent, not received.
                        break;
                    case VoiceOpCode.SelectProtocol:
                        // sent, not received
                        break;
                    case VoiceOpCode.Ready:
                        var ready = payload.Data.ToObject<VoiceReady>();
                        await HandleReadyAsync(ready!);
                        shouldResume = true;
                        break;
                    case VoiceOpCode.Heartbeat:
                        // sent, not received
                        break;
                    case VoiceOpCode.SessionDescription:
                        var sd = payload.Data.ToObject<VoiceSessionDescription>();
                        await HandleSessionDescription(sd!);
                        break;
                    case VoiceOpCode.Speaking:
                        // ignore for now
                        break;
                    case VoiceOpCode.HeartbeatAck:
                        _receivedAck = true;
                        break;
                    case VoiceOpCode.Resume:
                        // sent, not received
                        break;
                    case VoiceOpCode.Hello:
                        var hello = payload.Data.ToObject<VoiceHello>();
                        await HandleHelloAsync(hello!);
                        break;
                    case VoiceOpCode.Resumed:
                        shouldResume = true;
                        break;
                    case VoiceOpCode.ClientDisconnect:
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling payload with opcode {OpCode}: {Message}", payload.OpCode, ex.Message);
            }
        }
        private Task _ws_WebsocketClosed(string arg)
        {
            if (!string.IsNullOrWhiteSpace(arg))
            {
                Log.Warning("Voice Websocket closed: {Arg}", arg);
            }

            var hbt = _heartbeatTimer;
            hbt?.Change(Timeout.Infinite, Timeout.Infinite);
            _heartbeatTimer = null;

            if (!_stopCancellationToken.IsCancellationRequested && shouldResume)
            {
                _ = _ws.RunAndBlockAsync(_websocketUrl, _stopCancellationToken);
                return Task.CompletedTask;
            }
            
            _ws.WebsocketClosed -= _ws_WebsocketClosed;
            _ws.PayloadReceived -= _ws_PayloadReceived;
            
            if(!_stopCancellationToken.IsCancellationRequested)
                _stopCancellationSource.Cancel();

            return this.OnClosed(this);
        }

        public void SendRtpData(byte[] rtpData, int length)
        {
            _udpClient.Send(rtpData, length, _udpEp);
        }

        private Task HandleSessionDescription(VoiceSessionDescription sd)
        {
            SecretKey = sd.SecretKey;
            Mode = sd.Mode;

            _ = Task.Run(() => ConnectingFinished.TrySetResult(true));

            return Task.CompletedTask;
        }

        private Task ResumeAsync()
        {
            shouldResume = false;
            return SendCommandPayloadAsync(new VoicePayload
            {
                OpCode = VoiceOpCode.Resume,
                Data = JToken.FromObject(new VoiceResume
                {
                    ServerId = this._guildId.ToString(),
                    SessionId = this._sessionId,
                    Token = this._token,
                })
            });
        }

        private async Task HandleReadyAsync(VoiceReady ready)
        {
            Ssrc = ready.Ssrc;

            //Log.Information("Received ready {GuildId}, {Session}, {Token}", guildId, session, token);

            _udpEp = new IPEndPoint(IPAddress.Parse(ready.Ip), ready.Port);

            var ssrcBytes = BitConverter.GetBytes(Ssrc);
            var ipDiscoveryData = new byte[70];
            Buffer.BlockCopy(ssrcBytes, 0, ipDiscoveryData, 0, ssrcBytes.Length);
            await _udpClient.SendAsync(ipDiscoveryData, ipDiscoveryData.Length, _udpEp);
            while (true)
            {
                var buffer = _udpClient.Receive(ref _udpEp);

                if (buffer.Length == 70)
                {
                    //Log.Information("Received IP discovery data.");

                    var myIp = Encoding.UTF8.GetString(buffer, 4, buffer.Length - 8);
                    MyIp = myIp.TrimEnd('\0');
                    MyPort = BitConverter.ToUInt16(buffer, buffer.Length - 2);

                    //Log.Information("{MyIp}:{MyPort}", MyIp, MyPort);

                    await SelectProtocol();
                    return;
                }

                //Log.Information("Received voice data");
            }
        }

        private Task HandleHelloAsync(VoiceHello data)
        {
            _receivedAck = true;
            _heartbeatTimer = new Timer(async _ =>
            {
                await SendHeartbeatAsync();
            }, default, data.HeartbeatInterval, data.HeartbeatInterval);

            if (shouldResume)
            {
                return ResumeAsync();
            }

            return IdentifyAsync();
        }

        private Task IdentifyAsync()
            => SendCommandPayloadAsync(new VoicePayload
            {
                OpCode = VoiceOpCode.Identify,
                Data = JToken.FromObject(new VoiceIdentify
                {
                    ServerId = _guildId.ToString(),
                    SessionId = _sessionId,
                    Token = _token,
                    UserId = _userId.ToString(),
                })
            });

        private Task SelectProtocol()
            => SendCommandPayloadAsync(new VoicePayload
            {
                OpCode = VoiceOpCode.SelectProtocol,
                Data = JToken.FromObject(new SelectProtocol
                {
                    Protocol = "udp",
                    Data = new SelectProtocol.ProtocolData()
                    {
                        Address = MyIp,
                        Port = MyPort,
                        Mode = "xsalsa20_poly1305_lite",
                    }
                })
            });

        private async Task SendHeartbeatAsync()
        {
            if (!_receivedAck)
            {
                Log.Warning("Voice gateway didn't receive HearbeatAck - closing");
                var success = await _ws.CloseAsync();
                if (!success)
                    await _ws_WebsocketClosed(null);
                return;
            }
            
            _receivedAck = false;
            await SendCommandPayloadAsync(new VoicePayload
            {
                OpCode = VoiceOpCode.Heartbeat,
                Data = JToken.FromObject(_rng.Next())
            });
        }

        public Task SendSpeakingAsync(VoiceSpeaking.State speaking)
            => SendCommandPayloadAsync(new VoicePayload
            {
                OpCode = VoiceOpCode.Speaking,
                Data = JToken.FromObject(new VoiceSpeaking
                {
                    Delay = 0,
                    Ssrc = Ssrc,
                    Speaking = (int)speaking
                })
            });

        public Task StopAsync()
        {
            Started = false;
            shouldResume = false;
            if(!_stopCancellationSource.IsCancellationRequested)
                try { _stopCancellationSource.Cancel(); } catch { }
            return _ws.CloseAsync("Stopped by the user.");
        }

        public Task Start()
        {
            Started = true;
            _ = SendLoop();
            return _ws.RunAndBlockAsync(_websocketUrl, _stopCancellationToken);
        }

        public bool Started { get; set; }

        public async Task SendCommandPayloadAsync(VoicePayload payload)
        {
            var complete = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var queueItem = new QueueItem(payload, complete);

            if (!_channel.Writer.TryWrite(queueItem))
                await _channel.Writer.WriteAsync(queueItem);

            await complete.Task;
        }
    }
}
