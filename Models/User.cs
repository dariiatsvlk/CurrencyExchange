using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CurrencyExchanger.Models
{
    public class User
    {
        public int Id { get; set; } 
        public long ChatId { get; set; } 
        public string? Username { get; set; }
        public string BaseCurrency { get; set; } = "USD"; 
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
