using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KeyCertificates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CertificateId",
                table: "AuditEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificateThumbprint",
                table: "AuditEvents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Certificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EnrollmentId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Thumbprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SubjectDN = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IssuerDN = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NotBefore = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NotAfter = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CertificateDer = table.Column<byte[]>(type: "bytea", nullable: true),
                    ChainDer = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Certificates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_certificates_keyversion",
                table: "Certificates",
                column: "KeyVersionId",
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_certificates_thumbprint",
                table: "Certificates",
                column: "Thumbprint",
                unique: true,
                filter: "\"Thumbprint\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Certificates");

            migrationBuilder.DropColumn(
                name: "CertificateId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "CertificateThumbprint",
                table: "AuditEvents");
        }
    }
}
