using CurrencyExchanger.Utils;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CurrencyExchanger.Services
{
    public class ExchangeRateService
    {
        private readonly HttpClient _httpClient;
        private readonly string _appId = Constants.ExchangeRatesAppId; 
        private readonly ILogger<ExchangeRateService> _logger;

        public ExchangeRateService(ILogger<ExchangeRateService> logger)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(Constants.ExchangeRatesBaseUrl)
            };

            _logger = logger;
        }

        public async Task<Dictionary<string, decimal>?> GetRatesAsync(string baseCurrency, string[] targetCurrencies)
        {
            var symbols = string.Join(",", targetCurrencies);
            var url = $"latest.json?app_id={_appId}&base={baseCurrency}&symbols={symbols}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var errorJson = await response.Content.ReadAsStringAsync();
                Console.WriteLine(" API Error: " + errorJson);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("rates", out var ratesElement))
                return null;

            var rates = new Dictionary<string, decimal>();
            foreach (var currency in targetCurrencies)
            {
                if (ratesElement.TryGetProperty(currency, out var value))
                    rates[currency] = value.GetDecimal();
            }

            return rates;
        }

        public async Task<Dictionary<DateTime, decimal>?> GetHistoricalRatesAsync(string baseCurrency, string targetCurrency, int days)
        {
            var results = new Dictionary<DateTime, decimal>();
            var today = DateTime.UtcNow;

            for (int i = 0; i < days; i++)
            {
                var date = today.AddDays(-i).ToString("yyyy-MM-dd");
                var url = $"historical/{date}.json?app_id={_appId}&base={baseCurrency}&symbols={targetCurrency}";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) continue;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("rates", out var ratesElement) &&
                    ratesElement.TryGetProperty(targetCurrency, out var value))
                {
                    results[DateTime.Parse(date)] = value.GetDecimal();
                }
            }

            return results.Count > 0 ? results : null;
        }

        public async Task<Dictionary<string, decimal>?> GetRatesByDateAsync(string baseCurrency, string[] targets, DateTime date)
        {
            string url = $"https://openexchangerates.org/api/historical/{date:yyyy-MM-dd}.json?app_id={Constants.ExchangeRatesAppId}&base={baseCurrency}&symbols={string.Join(",", targets)}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("rates", out var ratesJson))
                    return null;

                var result = new Dictionary<string, decimal>();
                foreach (var symbol in targets)
                {
                    if (ratesJson.TryGetProperty(symbol, out var value))
                        result[symbol] = value.GetDecimal();
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при запиті курсу на дату {Date}", date);
                return null;
            }
        }

    }
}
