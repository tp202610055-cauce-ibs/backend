using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cauce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecommendationManualModifyArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "model_version_id",
                table: "recommendations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "archive_reason",
                table: "recommendations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "archived_at",
                table: "recommendations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "recommendations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "recommendations",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Backfill: las recomendaciones existentes provienen del motor (US14 CA03).
            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "recommendations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "engine_generated");

            migrationBuilder.AddColumn<string>(
                name: "steps",
                table: "recommendations",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "recommendations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "valid_until",
                table: "recommendations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_dummy",
                table: "model_versions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "archive_reason",
                table: "recommendations");

            migrationBuilder.DropColumn(
                name: "archived_at",
                table: "recommendations");

            migrationBuilder.DropColumn(
                name: "description",
                table: "recommendations");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "recommendations");

            migrationBuilder.DropColumn(
                name: "source",
                table: "recommendations");

            migrationBuilder.DropColumn(
                name: "steps",
                table: "recommendations");

            migrationBuilder.DropColumn(
                name: "title",
                table: "recommendations");

            migrationBuilder.DropColumn(
                name: "valid_until",
                table: "recommendations");

            migrationBuilder.DropColumn(
                name: "is_dummy",
                table: "model_versions");

            migrationBuilder.AlterColumn<Guid>(
                name: "model_version_id",
                table: "recommendations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
