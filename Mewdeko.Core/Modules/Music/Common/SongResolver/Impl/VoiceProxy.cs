using System;
using System.Threading.Tasks;
using Ayu.Discord.Voice;
using Ayu.Discord.Voice.Models;
using Serilog;

namespace Mewdeko.Modules.Music
{
    public sealed class VoiceProxy : IVoiceProxy
    {
        public enum VoiceProxyState
        {
            Created,
            Started,
            Stopped
        }

        private const int MAX_ERROR_COUNT = 20;
        private const int DELAY_ON_ERROR_MILISECONDS = 200;


        private VoiceGateway _gateway;

        public VoiceProxy(VoiceGateway initial)
        {
            _gateway = initial;
        }

        public VoiceProxyState State
            => _gateway switch
            {
                {Started: true, Stopped: false} => VoiceProxyState.Started,
                {Stopped: false} => VoiceProxyState.Created,
                _ => VoiceProxyState.Stopped
            };

        public bool SendPcmFrame(VoiceClient vc, Span<byte> data, int length)
        {
            try
            {
                var gw = _gateway;
                if (gw is null || gw.Stopped || !gw.Started) return false;

                vc.SendPcmFrame(gw, data, 0, length);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void SetGateway(VoiceGateway gateway)
        {
            _gateway = gateway;
        }

        public Task StartSpeakingAsync()
        {
            return RunGatewayAction(gw => gw.SendSpeakingAsync(VoiceSpeaking.State.Microphone));
        }

        public Task StopSpeakingAsync()
        {
            return RunGatewayAction(gw => gw.SendSpeakingAsync(VoiceSpeaking.State.None));
        }

        public async Task StartGateway()
        {
            await _gateway.Start();
        }

        public Task StopGateway()
        {
            if (_gateway is VoiceGateway gw)
                return gw.StopAsync();

            return Task.CompletedTask;
        }

        public async Task<bool> RunGatewayAction(Func<VoiceGateway, Task> action)
        {
            var errorCount = 0;
            do
            {
                if (State == VoiceProxyState.Stopped) break;

                try
                {
                    var gw = _gateway;
                    if (gw is null || !gw.ConnectingFinished.Task.IsCompleted)
                    {
                        ++errorCount;
                        await Task.Delay(DELAY_ON_ERROR_MILISECONDS);
                        Log.Debug("Gateway is not ready");
                        continue;
                    }

                    await action(gw);
                    errorCount = 0;
                }
                catch (Exception ex)
                {
                    ++errorCount;
                    await Task.Delay(DELAY_ON_ERROR_MILISECONDS);
                    Log.Debug(ex, "Error performing proxy gateway action");
                }
            } while (errorCount > 0 && errorCount <= MAX_ERROR_COUNT);

            return State != VoiceProxyState.Stopped && errorCount <= MAX_ERROR_COUNT;
        }
    }
}