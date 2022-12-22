using System.Reflection;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Mewdeko.Services.strings.impl;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Extensions;

public static class ServiceCollectionExtensions
{
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

    // consider using scrutor, because slightly different versions
    // of this might be needed in several different places
    public static IServiceCollection AddSealedSubclassesOf(this IServiceCollection services, Type baseType)
    {
        var subTypes = Assembly.GetCallingAssembly()
            .ExportedTypes
            .Where(type => type.IsSealed && baseType.IsAssignableFrom(type));

        foreach (var subType in subTypes) services.AddSingleton(baseType, subType);

        return services;
    }
}