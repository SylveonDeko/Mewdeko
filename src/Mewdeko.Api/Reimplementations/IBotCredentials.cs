using Discord;

namespace Mewdeko.WebApp.Reimplementations;

public interface IBotCredentials
{
    string? Token { get; }
    string? RedisOptions { get; }

    bool IsOwner(IUser u);
    bool IsOfficialMod(IUser u);
}

public class RestartConfig
{
    public RestartConfig(string cmd, string? args)
    {
        Cmd = cmd;
        Args = args;
    }

    public string Cmd { get; }
    public string Args { get; }
}

public class DbConfig
{
    public DbConfig(string? type, string? connectionString)
    {
        Type = type;
        ConnectionString = connectionString;
    }

    public string? Type { get; }
    public string? ConnectionString { get; }
}