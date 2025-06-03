using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CurrencyExchanger.Utils
{
    public static class Constants
    {
        public static string TelegramBotToken =>
            Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? throw new Exception("Missing TELEGRAM_BOT_TOKEN");

        public static string ExchangeRatesAppId =>
            Environment.GetEnvironmentVariable("EXCHANGE_RATES_APP_ID") ?? throw new Exception("Missing EXCHANGE_RATES_APP_ID");

        public static string ExchangeRatesBaseUrl =>
            Environment.GetEnvironmentVariable("EXCHANGE_RATES_BASE_URL") ?? "https://openexchangerates.org/api/";

        public static string DefaultBaseCurrency =>
            Environment.GetEnvironmentVariable("DEFAULT_BASE_CURRENCY") ?? "USD";

        public static string ApiBaseUrl =>
            Environment.GetEnvironmentVariable("API_BASE_URL") ?? "https://localhost:5001";

        public static string DbConnectionString =>
            Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") ?? throw new Exception("Missing DB_CONNECTION_STRING");
    }
}
