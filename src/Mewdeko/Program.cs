    using System.Net.Http;
    using Discord.Commands;
    using Discord.Interactions;
    using Fergun.Interactive;
    using Lavalink4NET.Extensions;
    using MartineApiNet;
    using Mewdeko.Common.Configs;
    using Mewdeko.Common.ModuleBehaviors;
    using Mewdeko.Common.PubSub;
    using Mewdeko.Database.DbContextStuff;
    using Mewdeko.Modules.Currency.Services;
    using Mewdeko.Modules.Currency.Services.Impl;
    using Mewdeko.Modules.Nsfw;
    using Mewdeko.Modules.Searches.Services;
    using Mewdeko.Services.Impl;
    using Mewdeko.Services.Settings;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NekosBestApiNet;
    using NsfwSpyNS;
    using Serilog;
    using ZiggyCreatures.Caching.Fusion;
    using RunMode = Discord.Commands.RunMode;

    namespace Mewdeko;

    /// <summary>
    /// The main entry point class for the Mewdeko application.
    /// </summary>
    public class Program
    {
        private static IDataCache Cache { get; set; }

        /// <summary>
        /// The entry point of the application.
        /// </summary>
        /// <param name="args">Command-line arguments passed to the application.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation of running the application.</returns>
        public static async Task Main(string[] args)
        {
            var settings = new HostApplicationBuilderSettings
            {
                ApplicationName = "Mewdeko"
            };
            var builder = Host.CreateEmptyApplicationBuilder(settings);
            var services = builder.Services;
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            LogSetup.SetupLogger("Mewdeko");
            var credentials = new BotCredentials();
            Cache = new RedisCache(credentials);

            if (!Uri.TryCreate(credentials.LavalinkUrl, UriKind.Absolute, out _))
            {
                Log.Error("The Lavalink URL is invalid! Please check the Lavalink URL in the configuration");
                Helpers.ReadErrorAndExit(5);
            }

            var migrationService = new MigrationService(
                null,
                credentials.Token,
                credentials.PsqlConnectionString,
                credentials.MigrateToPsql);

            await migrationService.ApplyMigrations(
                new MewdekoPostgresContext(new DbContextOptions<MewdekoPostgresContext>()));

            Log.Information("Waiting 5 seconds for migrations, if any...");
            await Task.Delay(5000);
            var client = new DiscordShardedClient(new DiscordSocketConfig
            {
                MessageCacheSize = 15,
                LogLevel = LogSeverity.Info,
                ConnectionTimeout = int.MaxValue,
                AlwaysDownloadUsers = true,
                GatewayIntents = GatewayIntents.All,
                FormatUsersInBidirectionalUnicode = false,
                LogGatewayIntentWarnings = false,
                DefaultRetryMode = RetryMode.RetryRatelimit
            });

            services.AddSingleton(client);

            services.AddSingleton(credentials);
            services.AddSingleton(Cache);
            services.AddSingleton(Cache.Redis);

            services
                .AddScoped<INsfwSpy, NsfwSpy>()
                .AddSingleton<FontProvider>()
                .AddSingleton<IBotCredentials>(credentials)
                .AddPooledDbContextFactory<MewdekoContext>(dbContextOptionsBuilder => dbContextOptionsBuilder
                    .UseNpgsql(credentials.PsqlConnectionString,
                        x => x.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
                    .EnableDetailedErrors()
                    .EnableSensitiveDataLogging())
                .AddSingleton<DbContextProvider>()
                .AddScoped<EventHandler>()
                .AddSingleton(new CommandService(new CommandServiceConfig
                {
                    CaseSensitiveCommands = false, DefaultRunMode = RunMode.Async, LogLevel = LogSeverity.Debug
                }))
                .AddSingleton(new MartineApi())
                .AddTransient<ISeria, JsonSeria>()
                .AddTransient<IPubSub, RedisPubSub>()
                .AddTransient<IConfigSeria, YamlSeria>()
                .AddSingleton(new InteractiveService(client, new InteractiveConfig
                {
                    ReturnAfterSendingPaginator = true
                }))
                .AddSingleton(new NekosBestApi())
                .AddSingleton(p => new InteractionService(p.GetRequiredService<DiscordShardedClient>()))
                .AddSingleton<Localization>()
                .AddSingleton<BotConfigService>()
                .AddSingleton<BotConfig>()
                .AddConfigServices()
                .AddBotStringsServices(credentials.TotalShards)
                .AddMemoryCache()
                .AddLavalink()
                .ConfigureLavalink(x =>
                {
                    x.Passphrase = "Hope4a11";
                    x.BaseAddress = new Uri(credentials.LavalinkUrl);
                })
                .AddScoped<ISearchImagesService, SearchImagesService>()
                .AddSingleton<ToneTagService>()
                .AddTransient<GuildSettingsService>();

            services.AddFusionCache().TryWithAutoSetup();

            if (credentials.UseGlobalCurrency)
            {
                services.AddTransient<ICurrencyService, GlobalCurrencyService>();
            }
            else
            {
                services.AddTransient<ICurrencyService, GuildCurrencyService>();
            }

            services.AddHttpClient();
            services.AddHttpClient("memelist").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            });

            services.Scan(scan => scan.FromAssemblyOf<IReadyExecutor>()
                .AddClasses(classes => classes.AssignableToAny(
                    typeof(INService),
                    typeof(IEarlyBehavior),
                    typeof(ILateBlocker),
                    typeof(IInputTransformer),
                    typeof(ILateExecutor)))
                .AsSelfWithInterfaces()
                .WithScopedLifetime()
            );

            services.AddSingleton<Mewdeko>()
                .AddHostedService<MewdekoService>();

            using var host = builder.Build();
            await host.RunAsync();
        }
    }