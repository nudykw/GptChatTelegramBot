using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataBaseLayer.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class CreateInitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BalanceHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    ModifiedById = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BalanceHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CachedTranslations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LanguageCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    ResourceKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    OriginalText = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    TranslatedText = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedTranslations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    ChatId = table.Column<long>(type: "INTEGER", nullable: false),
                    MessageId = table.Column<long>(type: "INTEGER", nullable: false),
                    ParentMessageId = table.Column<long>(type: "INTEGER", nullable: true),
                    RoleId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    FromUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ProviderName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ModelName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => new { x.ChatId, x.MessageId });
                });

            migrationBuilder.CreateTable(
                name: "TelegramChatInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    ChatType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    InviteLink = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramChatInfos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelegramUserInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    IsBot = table.Column<bool>(type: "INTEGER", nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    IsPremium = table.Column<bool>(type: "INTEGER", nullable: true),
                    PreferredProvider = table.Column<int>(type: "INTEGER", nullable: false),
                    LanguageCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    SelectedModel = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Balance = table.Column<decimal>(type: "TEXT", nullable: false),
                    BalanceModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastAiInteraction = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramUserInfos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AIBilingItem",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TelegramChatInfoId = table.Column<long>(type: "INTEGER", nullable: false),
                    TelegramUserInfoId = table.Column<long>(type: "INTEGER", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModelName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ProviderName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    PromptTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    CompletionTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    Cost = table.Column<decimal>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIBilingItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AIBilingItem_TelegramChatInfos_TelegramChatInfoId",
                        column: x => x.TelegramChatInfoId,
                        principalTable: "TelegramChatInfos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AIBilingItem_TelegramUserInfos_TelegramUserInfoId",
                        column: x => x.TelegramUserInfoId,
                        principalTable: "TelegramUserInfos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIBilingItem_TelegramChatInfoId",
                table: "AIBilingItem",
                column: "TelegramChatInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_AIBilingItem_TelegramUserInfoId",
                table: "AIBilingItem",
                column: "TelegramUserInfoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
