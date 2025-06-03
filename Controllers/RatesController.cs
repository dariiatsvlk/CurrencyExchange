using Microsoft.AspNetCore.Mvc;
using CurrencyExchanger.Services;
using CurrencyExchanger.Models;
using CurrencyExchanger.Utils;

namespace CurrencyExchanger.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RatesController : ControllerBase
    {
        private readonly ExchangeRateService _rateService;

        public RatesController(ExchangeRateService rateService)
        {
            _rateService = rateService;
        }

        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentRates(string baseCurrency = "USD")
        {
            var targets = CurrencyInfo.CurrencyDescriptions.Keys
                .Where(c => c != baseCurrency)
                .ToArray();

            var rates = await _rateService.GetRatesAsync(baseCurrency, targets);
            return rates == null
                ? NotFound("❌ Не вдалося отримати поточні курси.")
                : Ok(rates);
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(string from, string to, int days = 7)
        {
            var history = await _rateService.GetHistoricalRatesAsync(from, to, days);
            return history == null
                ? NotFound("❌ Не вдалося отримати історію курсів.")
                : Ok(history);
        }

        [HttpGet("ondate")]
        public async Task<IActionResult> GetRateOnDate(string from, string to, DateTime date)
        {
            var result = await _rateService.GetRatesByDateAsync(from, new[] { to }, date);

            if (result != null && result.TryGetValue(to, out var rate))
            {
                return Ok(new
                {
                    Date = date.ToString("yyyy-MM-dd"),
                    From = from,
                    To = to,
                    Rate = rate
                });
            }

            return NotFound(new
            {
                Message = "Курс не знайдено",
                From = from,
                To = to,
                Date = date.ToString("yyyy-MM-dd")
            });
        }


        [HttpGet("currencies")]
        public IActionResult GetCurrencyList()
        {
            return Ok(CurrencyInfo.CurrencyDescriptions);
        }
    }
}
