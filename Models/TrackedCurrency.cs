namespace CurrencyExchanger.Models
{
    public class TrackedCurrency
    {
        public int Id { get; set; }
        public long ChatId { get; set; } // Telegram ID 
        public string CurrencyCode { get; set; } = string.Empty;
    }
}
