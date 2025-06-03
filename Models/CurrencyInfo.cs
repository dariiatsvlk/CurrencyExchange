using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CurrencyExchanger.Models
{
    public class CurrencyInfo
    {
        public static readonly Dictionary<string, string> CurrencyDescriptions = new()
        {
            ["USD"] = "🇺🇸 USD — United States Dollar\nКраїна: США\nСимвол: $",
            ["EUR"] = "🇪🇺 EUR — Euro\nКраїна: Єврозона\nСимвол: €",
            ["UAH"] = "🇺🇦 UAH — Ukrainian Hryvnia\nКраїна: Україна\nСимвол: ₴",
            ["PLN"] = "🇵🇱 PLN — Polish Złoty\nКраїна: Польща\nСимвол: zł",
            ["GBP"] = "🇬🇧 GBP — British Pound\nКраїна: Велика Британія\nСимвол: £",
            ["JPY"] = "🇯🇵 JPY — Japanese Yen\nКраїна: Японія\nСимвол: ¥",
            ["CZK"] = "🇨🇿 CZK — Czech Koruna\nКраїна: Чехія\nСимвол: Kč",
            ["CHF"] = "🇨🇭 CHF — Swiss Franc\nКраїна: Швейцарія\nСимвол: ₣",
            ["SEK"] = "🇸🇪 SEK — Swedish Krona\nКраїна: Швеція\nСимвол: kr",
            ["TRY"] = "🇹🇷 TRY — Turkish Lira\nКраїна: Туреччина\nСимвол: ₺"
        };


    }
}
