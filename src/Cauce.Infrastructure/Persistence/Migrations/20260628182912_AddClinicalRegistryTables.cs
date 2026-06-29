using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cauce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicalRegistryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "custom_foods",
                columns: table => new
                {
                    custom_food_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    portion_size_grams = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custom_foods", x => x.custom_food_id);
                    table.CheckConstraint("ck_custom_foods_portion", "portion_size_grams > 0");
                    table.ForeignKey(
                        name: "fk_custom_foods_users_patient_id",
                        column: x => x.patient_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "food_items",
                columns: table => new
                {
                    food_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    calories_per_100g = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    protein_g_per_100g = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    carbs_g_per_100g = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    fat_g_per_100g = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    fiber_g_per_100g = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    fodmap_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    fodmap_tags = table.Column<string>(type: "text", nullable: true),
                    is_peruvian = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_food_items", x => x.food_id);
                    table.CheckConstraint("ck_food_items_calories", "calories_per_100g >= 0");
                    table.CheckConstraint("ck_food_items_carbs", "carbs_g_per_100g >= 0");
                    table.CheckConstraint("ck_food_items_fat", "fat_g_per_100g >= 0");
                    table.CheckConstraint("ck_food_items_fiber", "fiber_g_per_100g >= 0");
                    table.CheckConstraint("ck_food_items_protein", "protein_g_per_100g >= 0");
                });

            migrationBuilder.CreateTable(
                name: "ibs_sss_assessments",
                columns: table => new
                {
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cycle_number = table.Column<int>(type: "integer", nullable: false),
                    pain_severity = table.Column<int>(type: "integer", nullable: false),
                    pain_frequency = table.Column<int>(type: "integer", nullable: false),
                    bloating_severity = table.Column<int>(type: "integer", nullable: false),
                    bowel_habits_dissatisfaction = table.Column<int>(type: "integer", nullable: false),
                    life_interference = table.Column<int>(type: "integer", nullable: false),
                    total_score = table.Column<int>(type: "integer", nullable: false),
                    severity_category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    next_assessment_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ibs_sss_assessments", x => x.assessment_id);
                    table.CheckConstraint("ck_ibs_sss_bloating_severity", "bloating_severity BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_ibs_sss_bowel_habits", "bowel_habits_dissatisfaction BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_ibs_sss_cycle_number", "cycle_number >= 0");
                    table.CheckConstraint("ck_ibs_sss_life_interference", "life_interference BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_ibs_sss_pain_frequency", "pain_frequency BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_ibs_sss_pain_severity", "pain_severity BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_ibs_sss_total_score", "total_score BETWEEN 0 AND 500");
                    table.ForeignKey(
                        name: "fk_ibs_sss_assessments_users_patient_id",
                        column: x => x.patient_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meals",
                columns: table => new
                {
                    meal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_guid = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    meal_time = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sync_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    client_created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meals", x => x.meal_id);
                    table.ForeignKey(
                        name: "fk_meals_users_patient_id",
                        column: x => x.patient_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "custom_food_ingredients",
                columns: table => new
                {
                    ingredient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    custom_food_id = table.Column<Guid>(type: "uuid", nullable: false),
                    food_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proportion_grams = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custom_food_ingredients", x => x.ingredient_id);
                    table.CheckConstraint("ck_custom_food_ingredients_proportion", "proportion_grams > 0");
                    table.ForeignKey(
                        name: "fk_custom_food_ingredients_custom_foods_custom_food_id",
                        column: x => x.custom_food_id,
                        principalTable: "custom_foods",
                        principalColumn: "custom_food_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_custom_food_ingredients_food_items_food_id",
                        column: x => x.food_id,
                        principalTable: "food_items",
                        principalColumn: "food_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "meal_items",
                columns: table => new
                {
                    meal_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    meal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    food_id = table.Column<Guid>(type: "uuid", nullable: true),
                    custom_food_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meal_items", x => x.meal_item_id);
                    table.CheckConstraint("ck_meal_items_food_xor", "(food_id IS NULL) <> (custom_food_id IS NULL)");
                    table.CheckConstraint("ck_meal_items_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_meal_items_custom_foods_custom_food_id",
                        column: x => x.custom_food_id,
                        principalTable: "custom_foods",
                        principalColumn: "custom_food_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meal_items_food_items_food_id",
                        column: x => x.food_id,
                        principalTable: "food_items",
                        principalColumn: "food_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meal_items_meals_meal_id",
                        column: x => x.meal_id,
                        principalTable: "meals",
                        principalColumn: "meal_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "symptoms",
                columns: table => new
                {
                    symptom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_guid = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    symptom_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    intensity = table.Column<int>(type: "integer", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    associated_meal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    has_meal_association = table.Column<bool>(type: "boolean", nullable: false),
                    sync_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    client_created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_symptoms", x => x.symptom_id);
                    table.CheckConstraint("ck_symptoms_intensity", "intensity BETWEEN 1 AND 100");
                    table.ForeignKey(
                        name: "fk_symptoms_meals_associated_meal_id",
                        column: x => x.associated_meal_id,
                        principalTable: "meals",
                        principalColumn: "meal_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_symptoms_users_patient_id",
                        column: x => x.patient_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "clinical_notes",
                columns: table => new
                {
                    note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    meal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    symptom_id = table.Column<Guid>(type: "uuid", nullable: true),
                    content = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clinical_notes", x => x.note_id);
                    table.CheckConstraint("ck_clinical_notes_content", "length(content) > 0");
                    table.CheckConstraint("ck_clinical_notes_xor", "(meal_id IS NULL) <> (symptom_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_clinical_notes_meals_meal_id",
                        column: x => x.meal_id,
                        principalTable: "meals",
                        principalColumn: "meal_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_clinical_notes_symptoms_symptom_id",
                        column: x => x.symptom_id,
                        principalTable: "symptoms",
                        principalColumn: "symptom_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_clinical_notes_users_patient_id",
                        column: x => x.patient_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_clinical_notes_meal_id",
                table: "clinical_notes",
                column: "meal_id");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_notes_patient_id",
                table: "clinical_notes",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_notes_symptom_id",
                table: "clinical_notes",
                column: "symptom_id");

            migrationBuilder.CreateIndex(
                name: "ix_custom_food_ingredients_food_id",
                table: "custom_food_ingredients",
                column: "food_id");

            migrationBuilder.CreateIndex(
                name: "uq_ingredient_per_custom_food",
                table: "custom_food_ingredients",
                columns: new[] { "custom_food_id", "food_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_custom_foods_patient",
                table: "custom_foods",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "uq_custom_food_per_patient",
                table: "custom_foods",
                columns: new[] { "patient_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_food_items_category",
                table: "food_items",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_food_items_fodmap",
                table: "food_items",
                column: "fodmap_level");

            migrationBuilder.CreateIndex(
                name: "ix_food_items_is_active",
                table: "food_items",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ux_food_items_name",
                table: "food_items",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ibs_sss_assessments_patient_id",
                table: "ibs_sss_assessments",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_ibs_sss_assessments_patient_id1",
                table: "ibs_sss_assessments",
                column: "patient_id",
                unique: true,
                filter: "assessment_type = 'baseline'");

            migrationBuilder.CreateIndex(
                name: "ix_ibs_sss_severity",
                table: "ibs_sss_assessments",
                column: "severity_category");

            migrationBuilder.CreateIndex(
                name: "ix_ibs_sss_total_score",
                table: "ibs_sss_assessments",
                column: "total_score");

            migrationBuilder.CreateIndex(
                name: "ix_meal_items_custom_food_id",
                table: "meal_items",
                column: "custom_food_id");

            migrationBuilder.CreateIndex(
                name: "ix_meal_items_food_id",
                table: "meal_items",
                column: "food_id");

            migrationBuilder.CreateIndex(
                name: "ix_meal_items_meal_id",
                table: "meal_items",
                column: "meal_id");

            migrationBuilder.CreateIndex(
                name: "ix_meals_client_created_at",
                table: "meals",
                column: "client_created_at");

            migrationBuilder.CreateIndex(
                name: "ix_meals_patient_clientcrat",
                table: "meals",
                columns: new[] { "patient_id", "client_created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_meals_patient_id",
                table: "meals",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ux_meals_client_guid",
                table: "meals",
                column: "client_guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_symptoms_associated_meal_id",
                table: "symptoms",
                column: "associated_meal_id");

            migrationBuilder.CreateIndex(
                name: "ix_symptoms_client_created_at",
                table: "symptoms",
                column: "client_created_at");

            migrationBuilder.CreateIndex(
                name: "ix_symptoms_has_meal_association",
                table: "symptoms",
                column: "has_meal_association");

            migrationBuilder.CreateIndex(
                name: "ix_symptoms_patient_id",
                table: "symptoms",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ux_symptoms_client_guid",
                table: "symptoms",
                column: "client_guid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clinical_notes");

            migrationBuilder.DropTable(
                name: "custom_food_ingredients");

            migrationBuilder.DropTable(
                name: "ibs_sss_assessments");

            migrationBuilder.DropTable(
                name: "meal_items");

            migrationBuilder.DropTable(
                name: "symptoms");

            migrationBuilder.DropTable(
                name: "custom_foods");

            migrationBuilder.DropTable(
                name: "food_items");

            migrationBuilder.DropTable(
                name: "meals");
        }
    }
}
