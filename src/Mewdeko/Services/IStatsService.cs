namespace Mewdeko.Services;

public interface IStatsService : INService
{
    string Heap { get; }
    string Library { get; }
    long TextChannels { get; }
    long VoiceChannels { get; }
    string GetUptimeString(string separator = ", ");
    void Initialize();
}