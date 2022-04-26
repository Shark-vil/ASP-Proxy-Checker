using Microsoft.EntityFrameworkCore;
using ProxyChecker.Database.Models;

namespace ProxyChecker.Database
{
    public class DatabaseContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Config> Configuration { get; set; }
        public DbSet<FlareSolverrProxy> FlareSolverrProxies { get; set; }
        public DbSet<Proxy> Proxies { get; set; }
        public DbSet<BlockedProxy> BlockedProxies { get; set; }
        public DbSet<IPQualityScoreToken> IPQualityScoreTokens { get; set; }
        public DbSet<IPQualityScore> IPQualityScoreChecks { get; set; }

        public DatabaseContext() : base()
        {
            //Database.EnsureDeleted();
            Database.EnsureCreated();
        }

        /*
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //modelBuilder.Entity<Proxy>().HasKey(u => u.Id);

            modelBuilder.Entity<Config>().HasData(new Config
            {
                Id = 1,
                FlareSolverrUrl = "",
                AdminPassword = "",
                AdminUsername = ""
            });
        }
        */

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string databasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database.db");
            optionsBuilder.UseSqlite($"Filename={databasePath}");
            //optionsBuilder.LogTo(Console.WriteLine);
        }
    }
}
