using Mewdeko.Api.RedisCache;
using Mewdeko.Api.Reimplementations;
using Mewdeko.Api.Reimplementations.Impl;
using Mewdeko.Api.Reimplementations.PubSub;
using Mewdeko.Database;

var builder = WebApplication.CreateBuilder(args);
var creds = new BotCredentials();
var db = new DbService(1, creds.Token);
var cache = new RedisCache(creds);
// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddMvc();
builder.Services.AddSingleton<IBotCredentials>(creds);
builder.Services.AddSingleton(db);
builder.Services.AddSingleton(cache);
builder.Services.AddSingleton(cache.Redis);
builder.Services.AddDbContext<MewdekoContext>();
builder.Services.AddTransient<ISeria, JsonSeria>();
builder.Services.AddTransient<IPubSub, RedisPubSub>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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