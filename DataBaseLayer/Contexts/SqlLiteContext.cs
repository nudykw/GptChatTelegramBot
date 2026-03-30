using DataBaseLayer.Models;
using Microsoft.EntityFrameworkCore;

namespace DataBaseLayer.Contexts
{
    public class SqlLiteContext : DbContext
    {
        public SqlLiteContext() { }
        public SqlLiteContext(DbContextOptions<SqlLiteContext> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite(@"Data Source=StoreContext.db;Cache=Shared;Mode=ReadWriteCreate;Default Timeout=30;");
            }
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
        #endregion
    }
}
