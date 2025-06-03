using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using CurrencyExchanger.Utils;

namespace CurrencyExchanger.Data
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            var connectionString = Environment.GetEnvironmentVariable("DbConnectionString")
                ?? throw new InvalidOperationException("DbConnectionString not set");

            optionsBuilder.UseNpgsql(connectionString);

            return new AppDbContext(optionsBuilder.Options);
        }
    }

}
