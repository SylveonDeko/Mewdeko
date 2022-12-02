using Mewdeko.WebApp.Reimplementations;

namespace Mewdeko.WebApp.Extensions;

public static class StringExtensions
{
    public static string RedisKey(this IBotCredentials bc) => bc.Token[..10];
}