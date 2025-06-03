using Telegram.Bot;
using CurrencyExchanger.Services;
using CurrencyExchanger.Utils;
using CurrencyExchanger.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton(new TelegramBotClient(Constants.TelegramBotToken));
builder.Services.AddSingleton<TelegramBotService>();
builder.Services.AddSingleton<ExchangeRateService>();
builder.Services.AddHttpClient();
builder.Services.AddHostedService<DailyRateNotifier>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(Constants.DbConnectionString));




builder.Services.AddLogging(logging => logging.AddConsole());

var app = builder.Build();
app.Use((context, next) =>
{
    context.Request.Scheme = "https";
    return next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", context =>
{
    context.Response.Redirect("/swagger");
    return Task.CompletedTask;
});

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var botService = scope.ServiceProvider.GetRequiredService<TelegramBotService>();
    botService.Start();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

await app.RunAsync();
