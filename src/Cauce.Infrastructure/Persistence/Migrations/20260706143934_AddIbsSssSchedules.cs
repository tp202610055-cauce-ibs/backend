using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cauce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIbsSssSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ibs_sss_schedules",
                columns: table => new
                {
                    schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed = table.Column<bool>(type: "boolean", nullable: false),
                    missed = table.Column<bool>(type: "boolean", nullable: false),
                    reminder_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ibs_sss_schedules", x => x.schedule_id);
                    table.ForeignKey(
                        name: "fk_ibs_sss_schedules_users_patient_id",
                        column: x => x.patient_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ibs_sss_schedules_due_date",
                table: "ibs_sss_schedules",
                column: "due_date");

            migrationBuilder.CreateIndex(
                name: "ix_ibs_sss_schedules_patient_open",
                table: "ibs_sss_schedules",
                columns: new[] { "patient_id", "completed", "missed" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ibs_sss_schedules");
        }
    }
}
