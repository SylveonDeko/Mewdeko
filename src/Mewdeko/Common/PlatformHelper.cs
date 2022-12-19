namespace Mewdeko.Common;

public static class PlatformHelper
{
    private const int ProcessorCountRefreshIntervalMs = 30000;

    private static volatile int processorCount;
    private static volatile int lastProcessorCountRefreshTicks;

    public static int ProcessorCount
    {
        get
        {
            var now = Environment.TickCount;
            if (processorCount != 0 && now - lastProcessorCountRefreshTicks < ProcessorCountRefreshIntervalMs) return processorCount;
            processorCount = Environment.ProcessorCount;
            lastProcessorCountRefreshTicks = now;

            return processorCount;
        }
    }
}