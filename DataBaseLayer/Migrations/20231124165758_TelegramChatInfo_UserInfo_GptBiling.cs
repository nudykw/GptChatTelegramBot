using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataBaseLayer.Migrations
{
    /// <inheritdoc />
    public partial class TelegramChatInfo_UserInfo_GptBiling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    IsPremium = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramUserInfos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GptBilingItem",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TelegramChatInfoId = table.Column<long>(type: "INTEGER", nullable: false),
                    TelegramUserInfoId = table.Column<long>(type: "INTEGER", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModelName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    PromptTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    CompletionTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalTokens = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GptBilingItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GptBilingItem_TelegramChatInfos_TelegramChatInfoId",
                        column: x => x.TelegramChatInfoId,
                        principalTable: "TelegramChatInfos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GptBilingItem_TelegramUserInfos_TelegramUserInfoId",
                        column: x => x.TelegramUserInfoId,
                        principalTable: "TelegramUserInfos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GptBilingItem_TelegramChatInfoId",
                table: "GptBilingItem",
                column: "TelegramChatInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_GptBilingItem_TelegramUserInfoId",
                table: "GptBilingItem",
                column: "TelegramUserInfoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GptBilingItem");

            migrationBuilder.DropTable(
                name: "TelegramChatInfos");

            migrationBuilder.DropTable(
                name: "TelegramUserInfos");
        }
    }
}
