using Mewdeko.Api.Middleware;
using Mewdeko.Api.Services;
using Mewdeko.Api.Services.Impl;
using Mewdeko.Database;
using Npgsql;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var connectString = builder.Configuration.GetSection("ConnectionString").Value;
var skipApiKey = builder.Configuration.GetSection("SkipAuthorization").Value == "true";

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

    if (string.IsNullOrEmpty(sBuilder.Host))
    {
        Log.Error("No connection string provided. Exiting...");
        Environment.Exit(1);
    }
}
catch (Exception e)
{
    Log.Error("Invalid connection string provided. Exiting...");
    Environment.Exit(1);
}


var db = new DbService(0, null, true, connectString);

db.Setup();

builder.Services.AddSingleton(db);

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