using Mewdeko.Api.Middleware;
using Mewdeko.Api.Services;
using Mewdeko.Api.Services.Impl;
using Mewdeko.Database;
using Npgsql;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var connectString = builder.Configuration.GetSection("ConnectionString").Value;
var skipApiKey = builder.Configuration.GetSection("SkipAuthorization").Value == "true";
var redisUrl = builder.Configuration.GetSection("RedisUrl").Value;
var redisKey = builder.Configuration.GetSection("RedisKey").Value;

// Add services to the container.

var log = LogSetup.SetupLogger("Mewdeko.Api");
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(log);
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddTransient<IApiKeyValidation, ApiKeyValidation>();

try
{
    var sBuilder = new NpgsqlConnectionStringBuilder(connectString);

    if (connectString is null || string.IsNullOrEmpty(sBuilder.Host))
    {
        Log.Error("No connection string provided. Exiting...");
        Environment.Exit(1);
    }
}
catch
{
    Log.Error("Invalid connection string provided. Exiting...");
    Environment.Exit(1);
}


var db = new DbService(0, null, connectString);

await db.ApplyMigrations();

builder.Services.AddSingleton(db);

if (string.IsNullOrEmpty(redisUrl))
{
    Log.Error("Redis Url is empty. This is required for the api");
    Environment.Exit(1);
}

if (string.IsNullOrEmpty(redisKey))
{
    Log.Error("Redis Key is empty. This is required for the api");
    Environment.Exit(1);
}

var redis = new RedisCache(redisUrl, redisKey);

builder.Services.AddSingleton(redis);
builder.Services.AddSingleton(new GuildSettingsService(db, redis));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!skipApiKey)
{
    app.UseMiddleware<ApiKeyMiddleware>();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();