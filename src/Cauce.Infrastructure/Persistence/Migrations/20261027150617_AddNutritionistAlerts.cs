using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cauce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNutritionistAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "nutritionist_alerts",
                columns: table => new
                {
                    nutritionist_alert_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nutritionist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alert_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nutritionist_alerts", x => x.nutritionist_alert_id);
                    table.ForeignKey(
                        name: "fk_nutritionist_alerts_users_nutritionist_id",
                        column: x => x.nutritionist_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_nutritionist_alerts_users_patient_id",
                        column: x => x.patient_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_nutritionist_alerts_nutritionist_occurred",
                table: "nutritionist_alerts",
                columns: new[] { "nutritionist_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_nutritionist_alerts_patient",
                table: "nutritionist_alerts",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ux_nutritionist_alerts_event",
                table: "nutritionist_alerts",
                columns: new[] { "nutritionist_id", "patient_id", "alert_type", "occurred_at" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nutritionist_alerts");
        }
    }
}
