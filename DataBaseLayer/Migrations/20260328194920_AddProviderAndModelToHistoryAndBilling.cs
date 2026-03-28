using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataBaseLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderAndModelToHistoryAndBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModelName",
                table: "Messages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderName",
                table: "Messages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderName",
                table: "GptBilingItem",
                type: "TEXT",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModelName",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ProviderName",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ProviderName",
                table: "GptBilingItem");
        }
    }
}
