using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataBaseLayer.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class InitialSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BalanceHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ModifiedById = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BalanceHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CachedTranslations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LanguageCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ResourceKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OriginalText = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    TranslatedText = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedTranslations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    ParentMessageId = table.Column<long>(type: "bigint", nullable: true),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FromUserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProviderName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => new { x.ChatId, x.MessageId });
                });

            migrationBuilder.CreateTable(
                name: "TelegramChatInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ChatType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Username = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    InviteLink = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramChatInfos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelegramUserInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    IsBot = table.Column<bool>(type: "bit", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Username = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsPremium = table.Column<bool>(type: "bit", nullable: true),
                    PreferredProvider = table.Column<int>(type: "int", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SelectedModel = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAiInteraction = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramUserInfos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AIBilingItem",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TelegramChatInfoId = table.Column<long>(type: "bigint", nullable: false),
                    TelegramUserInfoId = table.Column<long>(type: "bigint", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PromptTokens = table.Column<int>(type: "int", nullable: true),
                    CompletionTokens = table.Column<int>(type: "int", nullable: true),
                    TotalTokens = table.Column<int>(type: "int", nullable: true),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
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
            migrationBuilder.DropTable(
                name: "AIBilingItem");

            migrationBuilder.DropTable(
                name: "BalanceHistories");

            migrationBuilder.DropTable(
                name: "CachedTranslations");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "TelegramChatInfos");

            migrationBuilder.DropTable(
                name: "TelegramUserInfos");
        }
    }
}
