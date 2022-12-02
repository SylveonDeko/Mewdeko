namespace Mewdeko.Votes.Services;

public interface IBotCredentials
{
    string Token { get; }
    string RedisOptions { get; }
}