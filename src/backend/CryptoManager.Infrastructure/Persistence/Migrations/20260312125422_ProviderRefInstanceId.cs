using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProviderRefInstanceId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderRef_ProviderInstanceId",
                table: "KeyVersions",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProviderRef_ProviderInstanceId",
                table: "KeyVersions");
        }
    }
}
