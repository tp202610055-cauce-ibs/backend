using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cauce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConsentDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consent_documents",
                columns: table => new
                {
                    consent_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    text_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consent_documents", x => x.consent_document_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_consent_documents_version",
                table: "consent_documents",
                column: "version",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consent_documents");
        }
    }
}
