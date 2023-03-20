using Mewdeko.Database;
using Mewdeko.WebApp.Reimplementations.Impl;

var builder = WebApplication.CreateBuilder(args);
var creds = new BotCredentials();
var db = new DbService(2, creds.Token);
// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddMvc();
builder.Services.AddSingleton(db);
builder.Services.AddDbContext<MewdekoContext>();
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