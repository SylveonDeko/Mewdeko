using Mewdeko.Api.Reimplementations;

namespace Mewdeko.Api.Extensions;

public static class StringExtensions
{
    public static string RedisKey(this IBotCredentials bc) => bc.Token[..10];
}