using DataBaseLayer.Models;
using Microsoft.EntityFrameworkCore;

namespace DataBaseLayer.Contexts
{
    public class SqlLiteContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(@"Data Source=StoreContext.db;");
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
