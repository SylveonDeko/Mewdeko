using Mewdeko.Database;

var builder = WebApplication.CreateBuilder(args);
var db = new DbService(2);
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