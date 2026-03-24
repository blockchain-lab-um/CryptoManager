using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditEventActorId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorId",
                table: "AuditEvents",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_Actor",
                table: "AuditEvents",
                column: "Actor");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActorId",
                table: "AuditEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActorId_Timestamp",
                table: "AuditEvents",
                columns: new[] { "ActorId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_Actor",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_ActorId",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_ActorId_Timestamp",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ActorId",
                table: "AuditEvents");
        }
    }
}
