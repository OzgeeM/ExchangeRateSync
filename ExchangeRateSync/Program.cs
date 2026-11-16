using System.Text.Json.Serialization;
using ExchangeRateSync.Clients;
using ExchangeRateSync.Data;
using ExchangeRateSync.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient<FrankfurterClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Frankfurter:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.Configure<SyncOptions>(builder.Configuration.GetSection("Sync"));
builder.Services.AddScoped<ExchangeRateSyncService>();
builder.Services.AddHostedService<ExchangeRateSyncWorker>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();