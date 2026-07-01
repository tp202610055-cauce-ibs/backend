using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cauce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecommendationsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "fructose_level",
                table: "food_items",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "lactose_level",
                table: "food_items",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "oligos_level",
                table: "food_items",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "polyols_level",
                table: "food_items",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.CreateTable(
                name: "model_versions",
                columns: table => new
                {
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    training_dataset_size = table.Column<int>(type: "integer", nullable: true),
                    performance_metrics = table.Column<string>(type: "jsonb", nullable: false),
                    deployed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deployed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_model_versions", x => x.version_id);
                });

            migrationBuilder.CreateTable(
                name: "recommendations",
                columns: table => new
                {
                    recommendation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewed_by_nutritionist_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    confidence_score = table.Column<decimal>(type: "numeric(4,3)", nullable: false),
                    auto_approved = table.Column<bool>(type: "boolean", nullable: false),
                    nutritionist_note = table.Column<string>(type: "text", nullable: true),
                    ai_explanation = table.Column<string>(type: "text", nullable: true),
                    explanation_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recommendations", x => x.recommendation_id);
                    table.ForeignKey(
                        name: "fk_recommendations_model_versions_model_version_id",
                        column: x => x.model_version_id,
                        principalTable: "model_versions",
                        principalColumn: "version_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recommendations_users_patient_id",
                        column: x => x.patient_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_recommendations_users_reviewed_by_nutritionist_id",
                        column: x => x.reviewed_by_nutritionist_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_feedback",
                columns: table => new
                {
                    feedback_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recommendation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    was_applied = table.Column<bool>(type: "boolean", nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sync_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recommendation_feedback", x => x.feedback_id);
                    table.ForeignKey(
                        name: "fk_recommendation_feedback_recommendations_recommendation_id",
                        column: x => x.recommendation_id,
                        principalTable: "recommendations",
                        principalColumn: "recommendation_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_items",
                columns: table => new
                {
                    recommendation_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recommendation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    food_id = table.Column<Guid>(type: "uuid", nullable: false),
                    substitute_food_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reasoning = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recommendation_items", x => x.recommendation_item_id);
                    table.ForeignKey(
                        name: "fk_recommendation_items_food_items_food_id",
                        column: x => x.food_id,
                        principalTable: "food_items",
                        principalColumn: "food_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recommendation_items_food_items_substitute_food_id",
                        column: x => x.substitute_food_id,
                        principalTable: "food_items",
                        principalColumn: "food_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recommendation_items_recommendations_recommendation_id",
                        column: x => x.recommendation_id,
                        principalTable: "recommendations",
                        principalColumn: "recommendation_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_food_items_fructose_level",
                table: "food_items",
                sql: "fructose_level BETWEEN 0 AND 2");

            migrationBuilder.AddCheckConstraint(
                name: "ck_food_items_lactose_level",
                table: "food_items",
                sql: "lactose_level BETWEEN 0 AND 2");

            migrationBuilder.AddCheckConstraint(
                name: "ck_food_items_oligos_level",
                table: "food_items",
                sql: "oligos_level BETWEEN 0 AND 2");

            migrationBuilder.AddCheckConstraint(
                name: "ck_food_items_polyols_level",
                table: "food_items",
                sql: "polyols_level BETWEEN 0 AND 2");

            migrationBuilder.CreateIndex(
                name: "ix_model_versions_only_one_active",
                table: "model_versions",
                column: "is_active",
                unique: true,
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "ux_model_versions_model_hash",
                table: "model_versions",
                column: "model_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_model_versions_version_name",
                table: "model_versions",
                column: "version_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_recommendation_feedback_recommendation_id",
                table: "recommendation_feedback",
                column: "recommendation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_items_food_id",
                table: "recommendation_items",
                column: "food_id");

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_items_recommendation_id",
                table: "recommendation_items",
                column: "recommendation_id");

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_items_substitute_food_id",
                table: "recommendation_items",
                column: "substitute_food_id");

            migrationBuilder.CreateIndex(
                name: "ix_recommendations_model_version_id",
                table: "recommendations",
                column: "model_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_recommendations_nutritionist_pending",
                table: "recommendations",
                columns: new[] { "reviewed_by_nutritionist_id", "status", "generated_at" },
                filter: "status = 'pending_review'");

            migrationBuilder.CreateIndex(
                name: "ix_recommendations_patient_status_generated",
                table: "recommendations",
                columns: new[] { "patient_id", "status", "generated_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recommendation_feedback");

            migrationBuilder.DropTable(
                name: "recommendation_items");

            migrationBuilder.DropTable(
                name: "recommendations");

            migrationBuilder.DropTable(
                name: "model_versions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_food_items_fructose_level",
                table: "food_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_food_items_lactose_level",
                table: "food_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_food_items_oligos_level",
                table: "food_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_food_items_polyols_level",
                table: "food_items");

            migrationBuilder.DropColumn(
                name: "fructose_level",
                table: "food_items");

            migrationBuilder.DropColumn(
                name: "lactose_level",
                table: "food_items");

            migrationBuilder.DropColumn(
                name: "oligos_level",
                table: "food_items");

            migrationBuilder.DropColumn(
                name: "polyols_level",
                table: "food_items");
        }
    }
}
