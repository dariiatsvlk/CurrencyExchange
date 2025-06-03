using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;
using Telegram.Bot.Types.ReplyMarkups;
using CurrencyExchanger.Models;
using CurrencyExchanger.Utils;
using CurrencyExchanger.Handlers;
using SixLabors.ImageSharp;
using CurrencyExchanger.Data;
using Microsoft.EntityFrameworkCore;


namespace CurrencyExchanger.Services
{
    public class TelegramBotService
    {
        private readonly TelegramBotClient _botClient;
        private readonly ILogger<TelegramBotService> _logger;
        private readonly HttpClient _http;
        private readonly Dictionary<long, (string From, string To)> _pendingRateDateRequests = new();
        private readonly Dictionary<long, (string From, string To)> _pendingConversions = new();
        private readonly IServiceScopeFactory _scopeFactory;

        public TelegramBotService(TelegramBotClient botClient, ILogger<TelegramBotService> logger, HttpClient http,
            IServiceScopeFactory scopeFactory)
        {
            _botClient = botClient;
            _logger = logger;
            _http = http;
            _scopeFactory = scopeFactory;
        }

        public void Start()
        {
            var cts = new CancellationTokenSource();

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };

            _botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken: cts.Token
            );

            var me = _botClient.GetMeAsync().Result;
            _logger.LogInformation("Бот запущено: @{Username}", me.Username);
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.Message && update.Message?.Type == MessageType.Text)
            {
                var text = update.Message.Text;
                var chatId = update.Message.Chat.Id;

                if (update.Message.ReplyToMessage != null && _pendingConversions.TryGetValue(chatId, out var pair))
                {
                    var input = update.Message.Text?.Trim().Replace(',', '.');

                    if (!decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                    {
                        await botClient.SendTextMessageAsync(chatId,
                            "❗ Введіть коректне число (наприклад: 100 або 100.50 / 100,50)");
                        return;
                    }

                    var url = $"{Constants.ApiBaseUrl}/api/convert?amount={amount.ToString(CultureInfo.InvariantCulture)}&from={pair.From}&to={pair.To}";
                    var response = await _http.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var result = JsonSerializer.Deserialize<ConvertResponse>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        await botClient.SendTextMessageAsync(chatId,
                            $"💱 {result.Amount} {result.From} ≈ {result.Result:F2} {result.To}");
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(chatId, "❌ Не вдалося виконати конвертацію.");
                    }

                    _pendingConversions.Remove(chatId);
                    return;
                }

                if (_pendingRateDateRequests.TryGetValue(chatId, out var ratePair))
                {
                    if (DateTime.TryParse(text, out var date))
                    {
                        var url = $"{Constants.ApiBaseUrl}/api/rates/ondate?from={ratePair.From}&to={ratePair.To}&date={date:yyyy-MM-dd}";
                        var response = await _http.GetAsync(url);

                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            var doc = JsonDocument.Parse(json);
                            var rate = doc.RootElement.GetProperty("rate").GetDecimal();

                            await botClient.SendTextMessageAsync(chatId,
                                $"📅 Курс {ratePair.From} → {ratePair.To} на {date:yyyy-MM-dd}:\n`1 {ratePair.From} = {rate:F4} {ratePair.To}`",
                                parseMode: ParseMode.Markdown);
                        }
                        else
                        {
                            await botClient.SendTextMessageAsync(chatId, "❌ Не вдалося отримати курс на вказану дату.");
                        }

                        _pendingRateDateRequests.Remove(chatId);
                    }

                    else
                    {
                        await botClient.SendTextMessageAsync(chatId, "❗ Невірний формат дати. Введіть у форматі YYYY-MM-DD.");
                    }

                    return;
                }

                if (text.StartsWith("/start"))
                {
                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "👋 Вітаю! Оберіть опцію:",
                        replyMarkup: MenuHandler.GetMainMenu(),
                        cancellationToken: cancellationToken
                    );
                    return;
                }

                if (text.StartsWith("/currency"))
                {
                    var args = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (args.Length == 2)
                    {
                        var code = args[1].ToUpper();
                        if (CurrencyInfo.CurrencyDescriptions.TryGetValue(code, out var description))
                        {
                            await botClient.SendTextMessageAsync(chatId, description);
                        }
                        else
                        {
                            await botClient.SendTextMessageAsync(chatId, $"❌ Валюта '{code}' не знайдена.");
                        }
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(chatId,
                            "❗ Приклад:\n`/currency USD`",
                            parseMode: ParseMode.Markdown);
                    }

                    return;
                }

                if (text.StartsWith("/currencies"))
                {
                    var response = await _http.GetAsync($"{Constants.ApiBaseUrl}/api/rates/currencies");
                    if (!response.IsSuccessStatusCode)
                    {
                        await botClient.SendTextMessageAsync(chatId, "❌ Не вдалося завантажити список валют.");
                        return;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    var message = "📘 Список основних валют:" + string.Join("\n", dict.Select(d => d.Value.Split('\n')[0]));

                    await botClient.SendTextMessageAsync(chatId, message);
                    return;
                }

                if (text.StartsWith("/help"))
                {
                    string help = "📖 Список доступних команд:\n" +
                                "/convert 100 EUR to UAH — конвертація\n" +
                                "/history USD EUR — історія курсів\n" +
                                "/compare EUR PLN — порівняння графіком\n" +
                                "/currency USD — опис валюти\n" +
                                "/rate USD UAH YYYY-MM-DD — курс на дату\n" +
                                "/track USD — додати до розсилки\n" +
                                "/untrack USD — прибрати з розсилки\n" +
                                "/tracked — ваш список валют\n" +
                                "/currencies — всі валюти\n" +
                                "/start — повернення в меню";

                    await botClient.SendTextMessageAsync(chatId, help);
                    return;
                }

                if (text.StartsWith("/history"))
                {
                    var args = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (args.Length == 3)
                    {
                        var from = args[1].ToUpper();
                        var to = args[2].ToUpper();

                        var url = $"{Constants.ApiBaseUrl}/api/rates/history?from={from}&to={to}";
                        var response = await _http.GetAsync(url);

                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            var dict = JsonSerializer.Deserialize<Dictionary<DateTime, decimal>>(json);

                            var message = $"📅 Історія курсу {from} → {to} за 7 днів:\n";
                            foreach (var entry in dict.OrderBy(e => e.Key))
                            {
                                message += $"{entry.Key:dd.MM.yyyy}: {entry.Value:F4}\n";
                            }

                            await botClient.SendTextMessageAsync(chatId, message);
                        }
                        else
                        {
                            await botClient.SendTextMessageAsync(chatId, "❌ Не вдалося отримати курс.");
                        }
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(chatId, "❗ Приклад:\n`/history USD EUR`", parseMode: ParseMode.Markdown);
                    }

                    return;
                }

                if (text.StartsWith("/convert"))
                {
                    var args = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (args.Length == 5 && args[3].ToLower() == "to")
                    {
                        var input = args[1].Trim().Replace(',', '.');

                        if (!decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal amount))
                        {
                            await botClient.SendTextMessageAsync(chatId,
                                "❗ Невірна сума. Наприклад: `100.5` або `100,5`", parseMode: ParseMode.Markdown);
                            return;
                        }

                        var from = args[2].ToUpper();
                        var to = args[4].ToUpper();

                        var url = $"{Constants.ApiBaseUrl}/api/convert?amount={amount.ToString(CultureInfo.InvariantCulture)}&from={from}&to={to}";
                        var response = await _http.GetAsync(url);

                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            var result = JsonSerializer.Deserialize<ConvertResponse>(json, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                            await botClient.SendTextMessageAsync(chatId,
                                $"💱 {result.Amount} {result.From} ≈ {result.Result:F2} {result.To}");
                        }
                        else
                        {
                            await botClient.SendTextMessageAsync(chatId, "❌ Не вдалося виконати конвертацію.");
                        }
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(chatId,
                            "❗ Формат команди:\n`/convert 100 EUR to UAH`",
                            parseMode: ParseMode.Markdown);
                    }

                    return;
                }

                if (text.StartsWith("/compare"))
                {
                    var args = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (args.Length == 3)
                    {
                        var from = args[1].ToUpper();
                        var to = args[2].ToUpper();

                        var urlFrom = $"{Constants.ApiBaseUrl}/api/rates/history?from=USD&to={from}";
                        var urlTo = $"{Constants.ApiBaseUrl}/api/rates/history?from=USD&to={to}";

                        var fromResp = await _http.GetAsync(urlFrom);
                        var toResp = await _http.GetAsync(urlTo);

                        if (!fromResp.IsSuccessStatusCode || !toResp.IsSuccessStatusCode)
                        {
                            await botClient.SendTextMessageAsync(chatId, "❌ Не вдалося отримати історію курсів.");
                            return;
                        }

                        var fromJson = await fromResp.Content.ReadAsStringAsync();
                        var toJson = await toResp.Content.ReadAsStringAsync();

                        var fromDict = JsonSerializer.Deserialize<Dictionary<DateTime, decimal>>(fromJson);
                        var toDict = JsonSerializer.Deserialize<Dictionary<DateTime, decimal>>(toJson);

                        if (fromDict == null || toDict == null || fromDict.Count == 0 || toDict.Count == 0)
                        {
                            await botClient.SendTextMessageAsync(chatId, "⚠️ Недостатньо даних для побудови графіка.");
                            return;
                        }

                        using var image = CurrencyGraphBuilder.BuildComparisonChart(fromDict, toDict, from, to);
                        using var stream = new MemoryStream();
                        image.SaveAsPng(stream);
                        stream.Position = 0;

                        await botClient.SendPhotoAsync(chatId, new Telegram.Bot.Types.InputFiles.InputOnlineFile(stream, $"{from}_vs_{to}.png"),
                            caption: $"📈 Порівняння динаміки {from} і {to} за 7 днів");
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(chatId, "❗ Приклад:\n`/compare EUR USD`", parseMode: ParseMode.Markdown);
                    }

                    return;
                }

                if (text.StartsWith("/rate"))
                {
                    var args = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (args.Length == 4 && DateTime.TryParse(args[3], out var date))
                    {
                        var from = args[1].ToUpper();
                        var to = args[2].ToUpper();

                        var url = $"{Constants.ApiBaseUrl}/api/rates/ondate?from={from}&to={to}&date={date:yyyy-MM-dd}";
                        var response = await _http.GetAsync(url);

                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            var obj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                            var rate = obj["rate"].GetDecimal();

                            await botClient.SendTextMessageAsync(chatId,
                                $"📅 Курс {from} → {to} на {date:yyyy-MM-dd}:\n`1 {from} = {rate:F4} {to}`",
                                parseMode: ParseMode.Markdown);
                        }
                        else
                        {
                            await botClient.SendTextMessageAsync(chatId, "❌ Не вдалося отримати курс.");
                        }
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(chatId, "❗ Формат:\n`/rate USD EUR 2024-01-01`", parseMode: ParseMode.Markdown);
                    }

                    return;
                }

                if (text.StartsWith("/tracked"))
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var currencies = await db.TrackedCurrencies
                        .Where(x => x.ChatId == chatId)
                        .Select(x => x.CurrencyCode)
                        .ToListAsync();

                    if (currencies.Count == 0)
                    {
                        await botClient.SendTextMessageAsync(chatId, "📭 Ви поки не відстежуєте жодну валюту.");
                    }
                    else
                    {
                        var message = "📋 Ви відстежуєте такі валюти:\n" + string.Join(", ", currencies);
                        await botClient.SendTextMessageAsync(chatId, message);
                    }

                    return;
                }

                if (text.StartsWith("/track"))
                {
                    var args = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (args.Length != 2)
                    {
                        await botClient.SendTextMessageAsync(chatId, "❗ Формат: `/track USD`", parseMode: ParseMode.Markdown);
                        return;
                    }

                    var code = args[1].ToUpper();

                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var already = await db.TrackedCurrencies
                        .AnyAsync(x => x.ChatId == chatId && x.CurrencyCode == code);

                    if (already)
                    {
                        await botClient.SendTextMessageAsync(chatId, $"🔔 Ви вже відстежуєте {code}");
                        return;
                    }

                    db.TrackedCurrencies.Add(new TrackedCurrency { ChatId = chatId, CurrencyCode = code });
                    await db.SaveChangesAsync();

                    await botClient.SendTextMessageAsync(chatId, $"✅ Валюта {code} додана до списку відстеження.");
                    return;
                }

                if (text.StartsWith("/untrack"))
                {
                    var args = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (args.Length != 2)
                    {
                        await botClient.SendTextMessageAsync(chatId, "❗ Формат: `/untrack USD`", parseMode: ParseMode.Markdown);
                        return;
                    }

                    var code = args[1].ToUpper();

                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var record = await db.TrackedCurrencies
                        .FirstOrDefaultAsync(x => x.ChatId == chatId && x.CurrencyCode == code);

                    if (record == null)
                    {
                        await botClient.SendTextMessageAsync(chatId, $"⚠️ Валюта {code} не була у списку.");
                        return;
                    }

                    db.TrackedCurrencies.Remove(record);
                    await db.SaveChangesAsync();

                    await botClient.SendTextMessageAsync(chatId, $"❌ Валюта {code} прибрана з відстеження.");
                    return;
                }
                
            }

            else if (update.Type == UpdateType.CallbackQuery)
            {
                var callback = update.CallbackQuery;
                var chatId = callback.Message.Chat.Id;
                var data = callback.Data;

                _logger.LogInformation("Callback: '{Data}' від @{User}", data, callback.From?.Username);

                switch (data)
                {
                    case "menu_back":
                        await botClient.SendTextMessageAsync(chatId, "🔙 Повернення до головного меню:",
                            replyMarkup: MenuHandler.GetMainMenu());
                        break;

                    case "rate":
                        await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: "📈 Оберіть опцію:",
                            replyMarkup: MenuHandler.GetRateSubMenu()
                        );
                        break;

                    case "rate_current":
                        {
                            var url = $"{Constants.ApiBaseUrl}/api/rates/current?baseCurrency=USD";
                            var response = await _http.GetAsync(url);

                            if (!response.IsSuccessStatusCode)
                            {
                                await botClient.SendTextMessageAsync(chatId, "❌ Не вдалося отримати поточні курси валют.");
                                break;
                            }

                            var json = await response.Content.ReadAsStringAsync();
                            var rates = JsonSerializer.Deserialize<Dictionary<string, decimal>>(json);

                            var message = "📊 Поточний курс відносно USD:\n";
                            foreach (var kv in rates.OrderBy(kv => kv.Key))
                            {
                                message += $"- 1 USD = {kv.Value:F4} {kv.Key}\n";
                            }

                            await botClient.SendTextMessageAsync(chatId, message);
                            break;
                        }

                    case "rate_date":
                        {
                            await botClient.SendTextMessageAsync(
                                chatId,
                                "📅 Оберіть базову валюту для перегляду курсу на дату:",
                                replyMarkup: MenuHandler.GetBaseCurrencyButtons("rate_date_base")
                            );
                            break;
                        }

                    case string rateBase when rateBase.StartsWith("rate_date_base_"):
                        {
                            var baseCurrency = rateBase.Replace("rate_date_base_", "");

                            await botClient.SendTextMessageAsync(
                                chatId,
                                $"📆 Оберіть валюту, до якої показати курс {baseCurrency}:",
                                replyMarkup: MenuHandler.GetTargetCurrencyButtons(baseCurrency, "rate_date")
                            );
                            break;
                        }

                    case string rateDate when rateDate.StartsWith("rate_date_"):
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(rateDate, @"^rate_date_([A-Z]{3})_([A-Z]{3})$");
                            if (match.Success)
                            {
                                var from = match.Groups[1].Value;
                                var to = match.Groups[2].Value;

                                _pendingRateDateRequests[chatId] = (from, to);

                                await botClient.SendTextMessageAsync(
                                    chatId,
                                    $"📅 Введіть дату для перегляду курсу {from} → {to} (у форматі YYYY-MM-DD):",
                                    replyMarkup: new ForceReplyMarkup { Selective = true }
                                );
                            }

                            break;
                        }

                    case "rate_history":
                        await botClient.SendTextMessageAsync(
                            chatId,
                            "📅 Оберіть базову валюту (відносно якої переглянути історію):",
                            replyMarkup: MenuHandler.GetBaseCurrencyButtons("history_base")
                        );
                        break;

                    case string histBase when histBase.StartsWith("history_base_"):
                        {
                            var baseCurrency = histBase.Replace("history_base_", "");

                            await botClient.SendTextMessageAsync(
                                chatId,
                                $"📊 Оберіть валюту, для якої переглянути історію відносно {baseCurrency}:",
                                replyMarkup: MenuHandler.GetTargetCurrencyButtons(baseCurrency, "history")
                            );
                            break;
                        }

                    case string hist when hist.StartsWith("history_"):
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(hist, @"^history_([A-Z]{3})_([A-Z]{3})$");
                            if (match.Success)
                            {
                                var from = match.Groups[1].Value;
                                var to = match.Groups[2].Value;

                                var url = $"{Constants.ApiBaseUrl}/api/rates/history?from={from}&to={to}";
                                var response = await _http.GetAsync(url);

                                if (!response.IsSuccessStatusCode)
                                {
                                    await botClient.SendTextMessageAsync(chatId, "❌ Не вдалося отримати курс.");
                                    break;
                                }

                                var json = await response.Content.ReadAsStringAsync();
                                var dict = JsonSerializer.Deserialize<Dictionary<DateTime, decimal>>(json);

                                var message = $"📅 Історія курсу {from} → {to} за 7 днів:\n";
                                foreach (var entry in dict.OrderBy(e => e.Key))
                                {
                                    message += $"{entry.Key:dd.MM.yyyy}: {entry.Value:F4}\n";
                                }

                                await botClient.SendTextMessageAsync(chatId, message);
                            }

                            break;
                        }

                    case "rate_compare":
                        await botClient.SendTextMessageAsync(
                            chatId,
                            "📊 Оберіть базову валюту для порівняння:",
                            replyMarkup: MenuHandler.GetBaseCurrencyButtons("compare_base")
                        );
                        break;

                    case string cmpBase when cmpBase.StartsWith("compare_base_"):
                        {
                            var baseCurrency = cmpBase.Replace("compare_base_", "");

                            await botClient.SendTextMessageAsync(
                                chatId,
                                $"📈 Оберіть іншу валюту для порівняння з {baseCurrency}:",
                                replyMarkup: MenuHandler.GetTargetCurrencyButtons(baseCurrency, "compare")
                            );
                            break;
                        }

                    case string cmp when cmp.StartsWith("compare_"):
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(cmp, @"^compare_([A-Z]{3})_([A-Z]{3})$");
                            if (match.Success)
                            {
                                var from = match.Groups[1].Value;
                                var to = match.Groups[2].Value;

                                var urlFrom = $"{Constants.ApiBaseUrl}/api/rates/history?from=USD&to={from}";
                                var urlTo = $"{Constants.ApiBaseUrl}/api/rates/history?from=USD&to={to}";

                                var fromResponse = await _http.GetAsync(urlFrom);
                                var toResponse = await _http.GetAsync(urlTo);

                                if (!fromResponse.IsSuccessStatusCode || !toResponse.IsSuccessStatusCode)
                                {
                                    await botClient.SendTextMessageAsync(chatId, "❌ Не вдалося отримати дані для порівняння.");
                                    break;
                                }

                                var fromJson = await fromResponse.Content.ReadAsStringAsync();
                                var toJson = await toResponse.Content.ReadAsStringAsync();

                                var fromDict = JsonSerializer.Deserialize<Dictionary<DateTime, decimal>>(fromJson);
                                var toDict = JsonSerializer.Deserialize<Dictionary<DateTime, decimal>>(toJson);

                                if (fromDict == null || toDict == null || fromDict.Count == 0 || toDict.Count == 0)
                                {
                                    await botClient.SendTextMessageAsync(chatId, "⚠️ Недостатньо даних для побудови графіка.");
                                    break;
                                }

                                using var image = CurrencyGraphBuilder.BuildComparisonChart(fromDict, toDict, from, to);
                                using var stream = new MemoryStream();
                                image.SaveAsPng(stream);
                                stream.Position = 0;

                                await botClient.SendPhotoAsync(chatId, new Telegram.Bot.Types.InputFiles.InputOnlineFile(stream, $"{from}_vs_{to}.png"),
                                    caption: $"📈 Порівняння динаміки {from} і {to} за 7 днів");
                            }

                            break;
                        }

                    case "convert":
                        await botClient.SendTextMessageAsync(
                            chatId,
                            "💱 Спочатку оберіть базову валюту:",
                            replyMarkup: MenuHandler.GetBaseCurrencyButtons("convert_base")
                        );
                        break;

                    case string baseSel when baseSel.StartsWith("convert_base_"):
                        {
                            var baseCurrency = baseSel.Replace("convert_base_", "");
                            await botClient.SendTextMessageAsync(
                                chatId,
                                $"💱 Оберіть валюту, в яку конвертувати {baseCurrency}:",
                                replyMarkup: MenuHandler.GetTargetCurrencyButtons(baseCurrency, "convert")
                            );
                            break;
                        }

                    case string conv when conv.StartsWith("convert_"):
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(conv, @"^convert_([A-Z]{3})_([A-Z]{3})$");
                            if (match.Success)
                            {
                                var from = match.Groups[1].Value;
                                var to = match.Groups[2].Value;

                                _pendingConversions[chatId] = (from, to);

                                await botClient.SendTextMessageAsync(
                                    chatId,
                                    $"✍️ Введіть суму для конвертації з {from} в {to}:",
                                    replyMarkup: new ForceReplyMarkup { Selective = true }
                                );
                            }

                            break;
                        }

                    case "currency_info":
                        await botClient.SendTextMessageAsync(chatId,
                            "📘 Оберіть валюту або введіть `/currency USD` вручну:",
                            parseMode: ParseMode.Markdown,
                            replyMarkup: MenuHandler.GetCurrencyInfoMenuDetailed());
                        break;

                    case string curr when curr.StartsWith("currency_"):
                        {
                            var code = curr.Replace("currency_", "");
                            if (CurrencyInfo.CurrencyDescriptions.TryGetValue(code, out var description))
                            {
                                await botClient.SendTextMessageAsync(chatId, description);
                            }
                            else
                            {
                                await botClient.SendTextMessageAsync(chatId, $"❌ Невідома валюта: {code}");
                            }
                            break;
                        }

                    case "show_all_currencies":
                        {
                            var message = "📘 Список основних валют:\n\n";
                            foreach (var pair in CurrencyInfo.CurrencyDescriptions)
                            {
                                var shortLine = pair.Value.Split('\n')[0];
                                message += $"{shortLine}\n";
                            }

                            await botClient.SendTextMessageAsync(chatId, message);
                            break;
                        }

                    case "notify_menu":
                        await botClient.SendTextMessageAsync(chatId, "📬 Меню розсилки:",
                            replyMarkup: MenuHandler.GetNotifyMenu());
                        break;

                    case "notify_list":
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                            var tracked = await db.TrackedCurrencies
                                .Where(x => x.ChatId == chatId)
                                .Select(x => x.CurrencyCode)
                                .ToListAsync();

                            if (tracked.Count == 0)
                                await botClient.SendTextMessageAsync(chatId, "📭 Ви поки не відстежуєте жодної валюти.");
                            else
                                await botClient.SendTextMessageAsync(chatId, "📋 Ваші валюти:\n" + string.Join(", ", tracked));

                            break;
                        }

                    case "notify_add":
                        {
                            await botClient.SendTextMessageAsync(
                                chatId,
                                "🔽 Оберіть валюту для додавання до щоденної розсилки:",
                                replyMarkup: MenuHandler.GetCurrencyListButtons("track_add")
                            );
                            break;
                        }

                    case string d when d.StartsWith("track_add_"):
                        {
                            var code = d.Replace("track_add_", "").ToUpper();

                            using var scope = _scopeFactory.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                            var already = await db.TrackedCurrencies
                                .AnyAsync(x => x.ChatId == chatId && x.CurrencyCode == code);

                            if (already)
                            {
                                await botClient.SendTextMessageAsync(chatId, $"🔔 Ви вже відстежуєте {code}");
                            }
                            else
                            {
                                db.TrackedCurrencies.Add(new TrackedCurrency { ChatId = chatId, CurrencyCode = code });
                                await db.SaveChangesAsync();

                                await botClient.SendTextMessageAsync(chatId, $"✅ Валюта {code} додана до розсилки.");
                            }

                            break;
                        }

                    case "notify_remove":
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                            var tracked = await db.TrackedCurrencies
                                .Where(x => x.ChatId == chatId)
                                .Select(x => x.CurrencyCode)
                                .ToListAsync();

                            if (tracked.Count == 0)
                            {
                                await botClient.SendTextMessageAsync(chatId, "📭 У вас немає валют для видалення.");
                            }
                            else
                            {
                                var markup = MenuHandler.GetTrackedCurrencyButtons("track_remove", tracked);
                                await botClient.SendTextMessageAsync(chatId, "➖ Оберіть валюту для видалення:", replyMarkup: markup);
                            }

                            break;
                        }

                    case string d when d.StartsWith("track_remove_"):
                        {
                            var code = d.Replace("track_remove_", "").ToUpper();

                            using var scope = _scopeFactory.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                            var record = await db.TrackedCurrencies
                                .FirstOrDefaultAsync(x => x.ChatId == chatId && x.CurrencyCode == code);

                            if (record == null)
                            {
                                await botClient.SendTextMessageAsync(chatId, $"⚠️ Валюта {code} не знайдена у списку.");
                            }
                            else
                            {
                                db.TrackedCurrencies.Remove(record);
                                await db.SaveChangesAsync();

                                await botClient.SendTextMessageAsync(chatId, $"❌ Валюта {code} успішно видалена з розсилки.");
                            }

                            break;
                        }

                    case "show_help":
                        {
                            string help = "📖 Список доступних команд:\n" +
                                "/convert 100 EUR to UAH — конвертація\n" +
                                "/history USD EUR — історія курсів\n" +
                                "/compare EUR PLN — порівняння графіком\n" +
                                "/currency USD — опис валюти\n" +
                                "/rate USD UAH YYYY-MM-DD — курс на дату\n" +
                                "/track USD — додати до розсилки\n" +
                                "/untrack USD — прибрати з розсилки\n" +
                                "/tracked — ваш список валют\n" +
                                "/currencies — всі валюти\n" +
                                "/start — повернення в меню";

                            await botClient.SendTextMessageAsync(chatId, help);
                            break;
                        }

                    default:
                        await botClient.AnswerCallbackQueryAsync(callback.Id, "Невідома дія.");
                        break;

                }
            }
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error: [{apiRequestException.ErrorCode}] {apiRequestException.Message}",
                _ => exception.ToString()
            };

            _logger.LogError("❌ Telegram API Error: {Message}", errorMessage);
            return Task.CompletedTask;
        } 
    }
}