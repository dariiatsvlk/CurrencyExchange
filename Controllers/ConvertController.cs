using Microsoft.AspNetCore.Mvc;
using CurrencyExchanger.Services;
using CurrencyExchanger.Utils;

namespace CurrencyExchanger.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConvertController : ControllerBase
    {
        private readonly ExchangeRateService _rateService;

        public ConvertController(ExchangeRateService rateService)
        {
            _rateService = rateService;
        }

        [HttpGet]
        public async Task<IActionResult> Convert(decimal amount, string from, string to)
        {
            var currencies = new[] { from, to }.Distinct().ToArray();
            var rates = await _rateService.GetRatesAsync("USD", currencies);

            if (rates == null || !rates.ContainsKey(from) || !rates.ContainsKey(to))
                return NotFound("❌ Не вдалося отримати курс для зазначених валют.");

            decimal usdAmount = amount / rates[from];
            decimal result = usdAmount * rates[to];

            return Ok(new
            {
                From = from,
                To = to,
                Amount = amount,
                Result = Math.Round(result, 2),
                Rate = Math.Round(result / amount, 4)
            });
        }
    }
}
