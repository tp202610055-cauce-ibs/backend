using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cauce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientsTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "allergies",
                columns: table => new
                {
                    allergy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    allergy_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_allergies", x => x.allergy_id);
                });

            migrationBuilder.CreateTable(
                name: "nutritionist_patient",
                columns: table => new
                {
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nutritionist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invitation_code_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    unassigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nutritionist_patient", x => x.assignment_id);
                    table.ForeignKey(
                        name: "fk_nutritionist_patient_invitation_codes_invitation_code_id",
                        column: x => x.invitation_code_id,
                        principalTable: "invitation_codes",
                        principalColumn: "code_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_nutritionist_patient_users_nutritionist_id",
                        column: x => x.nutritionist_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_nutritionist_patient_users_patient_id",
                        column: x => x.patient_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "patient_profiles",
                columns: table => new
                {
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    biological_sex = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    height_cm = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ibs_subtype = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    diagnosis_date = table.Column<DateOnly>(type: "date", nullable: true),
                    medications = table.Column<string>(type: "text", nullable: true),
                    onboarding_completed = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_patient_profiles", x => x.profile_id);
                    table.CheckConstraint("ck_patient_profiles_height_cm", "height_cm > 0 AND height_cm < 250");
                    table.CheckConstraint("ck_patient_profiles_weight_kg", "weight_kg > 0 AND weight_kg < 500");
                    table.ForeignKey(
                        name: "fk_patient_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "patient_allergies",
                columns: table => new
                {
                    patient_allergy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allergy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    declared_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_patient_allergies", x => x.patient_allergy_id);
                    table.ForeignKey(
                        name: "fk_patient_allergies_allergies_allergy_id",
                        column: x => x.allergy_id,
                        principalTable: "allergies",
                        principalColumn: "allergy_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_patient_allergies_users_patient_id",
                        column: x => x.patient_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_allergies_is_active",
                table: "allergies",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ux_allergies_name",
                table: "allergies",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_nutritionist_patient_invitation_code_id",
                table: "nutritionist_patient",
                column: "invitation_code_id");

            migrationBuilder.CreateIndex(
                name: "ix_nutritionist_patient_nutritionist_id",
                table: "nutritionist_patient",
                column: "nutritionist_id");

            migrationBuilder.CreateIndex(
                name: "ix_nutritionist_patient_patient_id",
                table: "nutritionist_patient",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "uq_nutritionist_patient_active",
                table: "nutritionist_patient",
                columns: new[] { "nutritionist_id", "patient_id" },
                unique: true,
                filter: "status = 'active'");

            migrationBuilder.CreateIndex(
                name: "ix_patient_allergies_allergy_id",
                table: "patient_allergies",
                column: "allergy_id");

            migrationBuilder.CreateIndex(
                name: "ix_patient_allergies_patient_id",
                table: "patient_allergies",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "uq_patient_allergy",
                table: "patient_allergies",
                columns: new[] { "patient_id", "allergy_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_patient_profiles_ibs_subtype",
                table: "patient_profiles",
                column: "ibs_subtype");

            migrationBuilder.CreateIndex(
                name: "ux_patient_profiles_user_id",
                table: "patient_profiles",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nutritionist_patient");

            migrationBuilder.DropTable(
                name: "patient_allergies");

            migrationBuilder.DropTable(
                name: "patient_profiles");

            migrationBuilder.DropTable(
                name: "allergies");
        }
    }
}
