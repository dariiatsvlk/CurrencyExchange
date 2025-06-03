using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CurrencyExchanger.Models;

namespace CurrencyExchanger.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<TrackedCurrency> TrackedCurrencies { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TrackedCurrency>()
                .HasIndex(tc => new { tc.ChatId, tc.CurrencyCode })
                .IsUnique();

            base.OnModelCreating(modelBuilder);
        }
    }
}