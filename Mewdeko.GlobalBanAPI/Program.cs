using System.Text.Json;
using Mewdeko.GlobalBanAPI.Common;
using Mewdeko.GlobalBanAPI.DbStuff;
using Mewdeko.GlobalBanAPI.DbStuff.Extensions;
using Microsoft.AspNetCore.RateLimiting;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("fixed", options =>
    {
        options.PermitLimit = 5;
        options.Window = TimeSpan.FromHours(1);
        options.QueueLimit = 0;
    });
});

builder.Services.AddSingleton<IConnectionMultiplexer>(x => ConnectionMultiplexer.Connect("localhost"));

var db = new DbService();
db.Setup();
builder.Services.AddSingleton(db);
builder.Services.AddScoped<ApiKeyAuthorizeFilter>();

builder.Services.AddControllers(options =>
{
    options.Filters.Add(typeof(ApiKeyAuthorizeFilter));
});

if (!File.Exists("config.yml"))
{
    File.WriteAllText("config.yml", "MasterKey: none");
}

var app = builder.Build();

// cache all bans in redis
var multiPlexer = app.Services.GetRequiredService<IConnectionMultiplexer>();
var redisDb = multiPlexer.GetDatabase();
var allBans = db.GetDbContext().GlobalBans.AllGlobalBans();
await redisDb.StringSetAsync("allBans", JsonSerializer.Serialize(allBans));

app.UseRateLimiter();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();