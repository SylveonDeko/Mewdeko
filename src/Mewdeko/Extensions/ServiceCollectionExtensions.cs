using System.Reflection;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Mewdeko.Services.strings.impl;
using Mewdeko.Services.Strings.Impl;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Extensions;

/// <summary>
/// Extension methods for IServiceCollection to register services related to bot strings and configuration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds services for bot strings based on the specified number of shards.
    /// </summary>
    /// <param name="services">The collection of services.</param>
    /// <param name="shardCount">The number of shards.</param>
    /// <returns>The modified collection of services.</returns>
    public static IServiceCollection AddBotStringsServices(this IServiceCollection services, int shardCount)
    {
        if (shardCount == 1)
        {
            return services.AddSingleton<IStringsSource, LocalFileStringsSource>()
                .AddSingleton<IBotStringsProvider, LocalBotStringsProvider>()
                .AddSingleton<IBotStrings, BotStrings>();
        }

        return services.AddSingleton<IStringsSource, LocalFileStringsSource>()
            .AddSingleton<IBotStringsProvider, RedisBotStringsProvider>()
            .AddSingleton<IBotStrings, BotStrings>();
    }

    /// <summary>
    /// Adds configuration services by scanning for sealed subclasses of <see cref="ConfigServiceBase{T}"/> in the calling assembly.
    /// </summary>
    /// <param name="services">The collection of services.</param>
    /// <returns>The modified collection of services.</returns>
    public static IServiceCollection AddConfigServices(this IServiceCollection services)
    {
        var baseType = typeof(ConfigServiceBase<>);

        foreach (var type in Assembly.GetCallingAssembly().ExportedTypes.Where(x => x.IsSealed))
        {
            if (type.BaseType?.IsGenericType != true || type.BaseType.GetGenericTypeDefinition() != baseType) continue;
            services.AddSingleton(type);
            services.AddSingleton(x => (IConfigService)x.GetRequiredService(type));
        }

        return services;
    }

    /// <summary>
    /// Adds services of sealed subclasses of the specified base type.
    /// </summary>
    /// <param name="services">The collection of services.</param>
    /// <param name="baseType">The base type whose subclasses are to be registered.</param>
    /// <returns>The modified collection of services.</returns>
    public static IServiceCollection AddSealedSubclassesOf(this IServiceCollection services, Type baseType)
    {
        var subTypes = Assembly.GetCallingAssembly()
            .ExportedTypes
            .Where(type => type.IsSealed && baseType.IsAssignableFrom(type));

        foreach (var subType in subTypes) services.AddSingleton(baseType, subType);

        return services;
    }
}