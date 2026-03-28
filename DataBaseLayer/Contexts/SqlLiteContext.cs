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
                optionsBuilder.UseSqlite(@"Data Source=StoreContext.db;");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HistoryMessage>()
            .HasKey(e => new { e.ChatId, e.MessageId });

            // Остальная конфигурация сущностей
            // ...

            base.OnModelCreating(modelBuilder);
        }

        #region models
        public DbSet<HistoryMessage> Messages { get; set; }
        public DbSet<GptBilingItem> GptBilingItem { get; set; }
        public DbSet<TelegramChatInfo> TelegramChatInfos { get; set; }
        public DbSet<TelegramUserInfo> TelegramUserInfos { get; set; }
        #endregion
    }
}
