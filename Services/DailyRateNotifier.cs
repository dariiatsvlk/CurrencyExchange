using CurrencyExchanger.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Microsoft.EntityFrameworkCore;
using CurrencyExchanger.Utils;

namespace CurrencyExchanger.Services
{
    public class DailyRateNotifier : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly TelegramBotClient _bot;
        private readonly ILogger<DailyRateNotifier> _logger;

        public DailyRateNotifier(IServiceProvider services, TelegramBotClient bot, ILogger<DailyRateNotifier> logger)
        {
            _services = services;
            _bot = bot;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var nextRun = DateTime.Today.AddHours(9);

                if (now > nextRun)
                    nextRun = nextRun.AddDays(1);

                var delay = nextRun - now;
                _logger.LogInformation("🕓 Наступна розсилка о {Time}", nextRun);
                await Task.Delay(delay, stoppingToken);

                await SendDailyRates(stoppingToken);
            }
        }

        private async Task SendDailyRates(CancellationToken token)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var http = scope.ServiceProvider.GetRequiredService<HttpClient>();

            var grouped = await db.TrackedCurrencies
                .GroupBy(t => t.ChatId)
                .ToListAsync(token);

            foreach (var group in grouped)
            {
                var chatId = group.Key;
                var codes = group.Select(x => x.CurrencyCode).Distinct().ToList();

                var url = $"{Constants.ApiBaseUrl}/api/rates/current?baseCurrency=USD";
                var response = await http.GetAsync(url, token);
                if (!response.IsSuccessStatusCode) continue;

                var json = await response.Content.ReadAsStringAsync(token);
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(json);

                var message = $"📬 Ваш щоденний курс валют (відносно USD):\n\n";
                foreach (var code in codes)
                {
                    if (dict != null && dict.TryGetValue(code, out var rate))
                    {
                        message += $"1 USD = {rate:F4} {code}\n";
                    }
                }

                if (message.Contains("="))
                {
                    await _bot.SendTextMessageAsync(chatId, message, cancellationToken: token);
                }
            }

            _logger.LogInformation("✅ Щоденна розсилка завершена");
        }
    }
}
