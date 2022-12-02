namespace Mewdeko.Services;

public interface IStatsService : INService
{
    string Heap { get; }
    string Library { get; }
    string GetUptimeString(string separator = ", ");
}