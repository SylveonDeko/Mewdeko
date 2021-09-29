using System;
using System.Threading.Tasks;
using Mewdeko.Voice;

namespace Mewdeko.Modules.Music
{
    public interface IVoiceProxy
    {
        VoiceProxy.VoiceProxyState State { get; }
        public bool SendPcmFrame(VoiceClient vc, Span<byte> data, int length);
        public void SetGateway(VoiceGateway gateway);
        Task StartSpeakingAsync();
        Task StopSpeakingAsync();
        public Task StartGateway();
        Task StopGateway();
    }
}