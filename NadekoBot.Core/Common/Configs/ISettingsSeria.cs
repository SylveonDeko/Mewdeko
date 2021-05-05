namespace NadekoBot.Core.Common.Configs
{
    public interface ISettingsSeria
    {
        public string Serialize<T>(T obj);
        public T Deserialize<T>(string data);
    }
}