using DataBaseLayer.Models;
using Microsoft.EntityFrameworkCore;

namespace DataBaseLayer.Contexts
{
    public class StoreContext : DbContext
    {
        public StoreContext() { }
        public StoreContext(DbContextOptions<StoreContext> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Configuration is now handled in MigrationConfigurator or Program.cs via DI
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HistoryMessage>()
            .HasKey(e => new { e.ChatId, e.MessageId });

            // Additional entity configuration
            // ...

            base.OnModelCreating(modelBuilder);
        }

        #region models
        public DbSet<HistoryMessage> Messages { get; set; }
        public DbSet<AIBilingItem> AIBilingItem { get; set; }
        public DbSet<TelegramChatInfo> TelegramChatInfos { get; set; }
        public DbSet<TelegramUserInfo> TelegramUserInfos { get; set; }
        public DbSet<CachedTranslation> CachedTranslations { get; set; }
        public DbSet<BalanceHistory> BalanceHistories { get; set; }
        public DbSet<CachedAIModel> CachedAIModels { get; set; }
        #endregion
    }
}
