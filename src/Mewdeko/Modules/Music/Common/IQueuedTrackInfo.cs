namespace Mewdeko.Modules.Music.Common
{
    public interface IQueuedTrackInfo : ITrackInfo
    {
        public ITrackInfo TrackInfo { get; }

        public string Queuer { get; }
    }
}