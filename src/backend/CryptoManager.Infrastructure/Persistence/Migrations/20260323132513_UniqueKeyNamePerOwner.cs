using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniqueKeyNamePerOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Keys_Name",
                table: "Keys");

            migrationBuilder.CreateIndex(
                name: "IX_Keys_Name_OwnerId",
                table: "Keys",
                columns: new[] { "Name", "OwnerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Keys_Name_OwnerId",
                table: "Keys");

            migrationBuilder.CreateIndex(
                name: "IX_Keys_Name",
                table: "Keys",
                column: "Name",
                unique: true);
        }
    }
}
