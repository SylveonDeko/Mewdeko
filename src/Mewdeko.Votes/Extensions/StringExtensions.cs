using Mewdeko.Votes.Services;

namespace Mewdeko.Votes.Extensions;

public static class StringExtensions
{
    public static string RedisKey(this IBotCredentials bc) => bc.Token[..10];
}