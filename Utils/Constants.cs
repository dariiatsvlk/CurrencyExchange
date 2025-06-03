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
        Environment.GetEnvironmentVariable("TelegramBotToken") ?? throw new Exception("Missing TelegramBotToken");

    public static string ExchangeRatesAppId =>
        Environment.GetEnvironmentVariable("ExchangeRatesAppId") ?? throw new Exception("Missing ExchangeRatesAppId");

    public static string ExchangeRatesBaseUrl =>
        Environment.GetEnvironmentVariable("ExchangeRatesBaseUrl") ?? "https://openexchangerates.org/api/";

    public static string DefaultBaseCurrency => "USD";

    public static string ApiBaseUrl =>
        Environment.GetEnvironmentVariable("ApiBaseUrl") ?? "https://localhost:5001";

    public static string DbConnectionString =>
        Environment.GetEnvironmentVariable("DbConnectionString") ?? throw new Exception("Missing DbConnectionString");
}

}
