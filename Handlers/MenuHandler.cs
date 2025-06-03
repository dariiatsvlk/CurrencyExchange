using CurrencyExchanger.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace CurrencyExchanger.Handlers
{
    public static class MenuHandler
    {
        // /start -> Головне меню
        public static InlineKeyboardMarkup GetMainMenu()
        {
            return new InlineKeyboardMarkup(new[]
            {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📈 Курси валют", "rate"),
                InlineKeyboardButton.WithCallbackData("💱 Конвертація", "convert")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("ℹ️ Інформація про валюту", "currency_info"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📖 Команди", "show_help")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📬 Щоденна розсилка", "notify_menu")
            }
        });
        }


        // /start -> Головне меню -> Курс валют -> підменю
        public static InlineKeyboardMarkup GetRateSubMenu()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("📊 Поточний курс", "rate_current") },
                new[] {InlineKeyboardButton.WithCallbackData("📅 Курс на дату", "rate_date") },
                new[] { InlineKeyboardButton.WithCallbackData("📅 Історія за 7 днів", "rate_history") },
                new[] { InlineKeyboardButton.WithCallbackData("📈 Порівняння 2 валют", "rate_compare") },
                new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "menu_back") }
            });
        }


        // /start -> Головне меню -> Курс валют -> Історія за 7 днів -> підменю
        public static InlineKeyboardMarkup GetBaseCurrencyButtonsForHistory()
        {
            return MenuHandler.GetBaseCurrencyButtons("history_base");
        }

        public static InlineKeyboardMarkup GetTargetCurrencyButtonsForHistory(string baseCurrency)
        {
            return MenuHandler.GetTargetCurrencyButtons(baseCurrency, "history");
        }

        // /start -> Головне меню -> Курс валют -> Курс на дату -> валюта
        public static InlineKeyboardMarkup GetBaseCurrencyButtonsForDate()
        {
            return MenuHandler.GetBaseCurrencyButtons("rate_date_base");
        }

        public static InlineKeyboardMarkup GetTargetCurrencyButtonsForDate(string baseCurrency)
        {
            return MenuHandler.GetTargetCurrencyButtons(baseCurrency, "rate_date");
        }


        // /start -> Головне меню -> Конвертація
        public static InlineKeyboardMarkup GetPopularConversionButtons()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("USD → EUR", "convert_USD_EUR"),
                    InlineKeyboardButton.WithCallbackData("EUR → UAH", "convert_EUR_UAH")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("UAH → PLN", "convert_UAH_PLN"),
                    InlineKeyboardButton.WithCallbackData("USD → GBP", "convert_USD_GBP")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🔙 Назад", "menu_back")
                }
            });
        }


        // /start -> Головне меню -> Курс валют -> Порівняння 2 валют -> підменю
        public static InlineKeyboardMarkup GetBaseCurrencyButtonsForComparison()
        {
            return MenuHandler.GetBaseCurrencyButtons("compare_base");
        }
        public static InlineKeyboardMarkup GetTargetCurrencyButtonsForComparison(string baseCurrency)
        {
            return MenuHandler.GetTargetCurrencyButtons(baseCurrency, "compare");
        }


        // /start -> Головне меню -> Інформація про валюту -> підменю
        public static InlineKeyboardMarkup GetCurrencyInfoMenuDetailed()
        {
            var buttons = CurrencyInfo.CurrencyDescriptions.Keys
                .OrderBy(c => c)
                .Select(code => InlineKeyboardButton.WithCallbackData(code, $"currency_{code}"))
                .Chunk(3)
                .Select(row => row.ToArray())
                .ToList();

            buttons.Insert(0, new[] {
                InlineKeyboardButton.WithCallbackData("📋 Усі валюти", "show_all_currencies")
            });

            buttons.Add(new[] {
                InlineKeyboardButton.WithCallbackData("🔙 Назад", "menu_back")
            });

            return new InlineKeyboardMarkup(buttons);
        }


        // /start -> Головне меню -> Курс валют -> підменю
        public static InlineKeyboardMarkup GetRateOnDateMenu()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("USD → EUR", "rate_date_USD_EUR"),
                    InlineKeyboardButton.WithCallbackData("USD → UAH", "rate_date_USD_UAH")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("EUR → PLN", "rate_date_EUR_PLN"),
                    InlineKeyboardButton.WithCallbackData("GBP → UAH", "rate_date_GBP_UAH")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📋 Ввести свою пару", "rate_date_custom"),
                    InlineKeyboardButton.WithCallbackData("🔙 Назад", "rate")
                }
            });
        }

        // 1st currency buttons
        public static InlineKeyboardMarkup GetBaseCurrencyButtons(string actionPrefix)
        {
            var buttons = CurrencyInfo.CurrencyDescriptions.Keys
                .Select(c => InlineKeyboardButton.WithCallbackData(c, $"{actionPrefix}_{c}"))
                .Chunk(3) 
                .Select(row => row.ToArray())
                .ToList();

            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "menu_back") });

            return new InlineKeyboardMarkup(buttons);
        }

        // 2nd currency buttons
        public static InlineKeyboardMarkup GetTargetCurrencyButtons(string baseCurrency, string context)
        {
            var buttons = new List<List<InlineKeyboardButton>>();
            var row = new List<InlineKeyboardButton>();

            foreach (var code in CurrencyInfo.CurrencyDescriptions.Keys.OrderBy(c => c))
            {
                if (code == baseCurrency) continue;

                row.Add(InlineKeyboardButton.WithCallbackData(code, $"{context}_{baseCurrency}_{code}"));

                if (row.Count == 3)
                {
                    buttons.Add(row);
                    row = new List<InlineKeyboardButton>();
                }
            }

            if (row.Count > 0)
                buttons.Add(row);

            var backCallback = context switch
            {
                "compare" => "rate_compare",
                "history" => "rate_history",
                "convert" => "convert",
                "rate_date" => "rate_date",
                _ => "menu_back"
            };

            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData("🔙 Назад", backCallback)
            });

            return new InlineKeyboardMarkup(buttons);
        }


        // /start -> Головне меню -> Щоденна розсилка
        public static InlineKeyboardMarkup GetNotifyMenu()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("➕ Додати валюту", "notify_add"),
                    InlineKeyboardButton.WithCallbackData("➖ Видалити валюту", "notify_remove")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📋 Перегляд валют", "notify_list"),
                    InlineKeyboardButton.WithCallbackData("🔙 Назад", "menu_back")
                }
            });
        }
        public static InlineKeyboardMarkup GetCurrencyListButtons(string prefix)
        {
            var buttons = new List<List<InlineKeyboardButton>>();
            var row = new List<InlineKeyboardButton>();

            foreach (var code in CurrencyInfo.CurrencyDescriptions.Keys.OrderBy(c => c))
            {
                row.Add(InlineKeyboardButton.WithCallbackData(code, $"{prefix}_{code}"));

                if (row.Count == 3)
                {
                    buttons.Add(row);
                    row = new List<InlineKeyboardButton>();
                }
            }

            if (row.Count > 0)
                buttons.Add(row);

            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData("🔙 Назад", "notify_menu")
            });

            return new InlineKeyboardMarkup(buttons);
        }
        public static InlineKeyboardMarkup GetTrackedCurrencyButtons(string prefix, List<string> tracked)
        {
            var buttons = new List<List<InlineKeyboardButton>>();
            var row = new List<InlineKeyboardButton>();

            foreach (var code in tracked.OrderBy(c => c))
            {
                row.Add(InlineKeyboardButton.WithCallbackData(code, $"{prefix}_{code}"));

                if (row.Count == 3)
                {
                    buttons.Add(row);
                    row = new List<InlineKeyboardButton>();
                }
            }

            if (row.Count > 0)
                buttons.Add(row);

            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData("🔙 Назад", "notify_menu")
            });

            return new InlineKeyboardMarkup(buttons);
        }

    }
}
