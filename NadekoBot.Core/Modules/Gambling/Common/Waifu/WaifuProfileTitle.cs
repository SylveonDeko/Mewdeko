namespace NadekoBot.Core.Modules.Gambling.Common.Waifu
{
    public struct WaifuProfileTitle
    {
        public int Count { get; }
        public string Title { get; }

        public WaifuProfileTitle(int count, string title)
        {
            Count = count;
            Title = title;
        }
    }
}
