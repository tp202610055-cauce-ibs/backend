using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cauce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditingNotificationsWorkersReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "fcm_token",
                table: "users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "clinical_reports_metadata",
                columns: table => new
                {
                    clinical_report_metadata_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    generated_by_nutritionist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    object_storage_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_size_bytes = table.Column<int>(type: "integer", nullable: false),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clinical_reports_metadata", x => x.clinical_report_metadata_id);
                    table.ForeignKey(
                        name: "fk_clinical_reports_metadata_users_generated_by_nutritionist_id",
                        column: x => x.generated_by_nutritionist_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_clinical_reports_metadata_users_patient_id",
                        column: x => x.patient_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    channel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    retry_count = table.Column<short>(type: "smallint", nullable: false),
                    related_entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    related_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scheduled_for = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.notification_id);
                    table.ForeignKey(
                        name: "fk_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    outbox_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<short>(type: "smallint", nullable: false),
                    last_error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.outbox_message_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_clinical_reports_metadata_generated_by_nutritionist_id",
                table: "clinical_reports_metadata",
                column: "generated_by_nutritionist_id");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_reports_metadata_patient_generated",
                table: "clinical_reports_metadata",
                columns: new[] { "patient_id", "generated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_dispatch",
                table: "notifications",
                columns: new[] { "status", "scheduled_for" },
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ux_notifications_dedup",
                table: "notifications",
                columns: new[] { "user_id", "type", "related_entity_type", "related_entity_id" },
                unique: true,
                filter: "related_entity_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                table: "outbox_messages",
                column: "occurred_at",
                filter: "processed_at IS NULL");

            // Auditoría a nivel de base de datos (DEC-B5-03, capa 4). pgcrypto provee digest() para
            // los hashes SHA-256 de los valores antes/después (acta A5).
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

            // Función de auditoría genérica: escribe una fila en audit_logs por cada I/U/D de las
            // tablas críticas. El actor se toma de la variable de sesión cauce.actor_user_id que
            // propaga el interceptor (acta A1); los valores se resumen con SHA-256, nunca en claro.
            // TG_ARGV[0] = nombre del tipo de entidad; TG_ARGV[1] = nombre de la columna de clave
            // primaria. Las acciones se escriben como 'create'/'update'/'delete' para casar con el
            // enum AuditActionType (acta A4).
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION audit_trigger_fn()
                RETURNS TRIGGER AS $$
                DECLARE
                    v_actor uuid;
                    v_action varchar(50);
                    v_entity_id uuid;
                    v_old_hash varchar(64);
                    v_new_hash varchar(64);
                    v_entity_type varchar(100);
                    v_pk_column text;
                BEGIN
                    v_actor := NULLIF(current_setting('cauce.actor_user_id', true), '')::uuid;
                    v_entity_type := TG_ARGV[0];
                    v_pk_column := TG_ARGV[1];

                    IF (TG_OP = 'INSERT') THEN
                        v_action := 'create';
                        v_new_hash := encode(digest(row_to_json(NEW)::text, 'sha256'), 'hex');
                        v_entity_id := (row_to_json(NEW) ->> v_pk_column)::uuid;
                    ELSIF (TG_OP = 'UPDATE') THEN
                        v_action := 'update';
                        v_old_hash := encode(digest(row_to_json(OLD)::text, 'sha256'), 'hex');
                        v_new_hash := encode(digest(row_to_json(NEW)::text, 'sha256'), 'hex');
                        v_entity_id := (row_to_json(NEW) ->> v_pk_column)::uuid;
                    ELSE
                        v_action := 'delete';
                        v_old_hash := encode(digest(row_to_json(OLD)::text, 'sha256'), 'hex');
                        v_entity_id := (row_to_json(OLD) ->> v_pk_column)::uuid;
                    END IF;

                    INSERT INTO audit_logs (
                        audit_log_id, actor_user_id, action_type, entity_type, entity_id,
                        old_values_hash, new_values_hash, ip_address, user_agent, additional_context, occurred_at)
                    VALUES (
                        gen_random_uuid(), v_actor, v_action, v_entity_type, v_entity_id,
                        v_old_hash, v_new_hash, NULL, NULL, jsonb_build_object('source', 'trigger'),
                        now() at time zone 'utc');

                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;");

            CreateAuditTrigger(migrationBuilder, "recommendations", "Recommendation", "recommendation_id");
            CreateAuditTrigger(migrationBuilder, "meals", "Meal", "meal_id");
            CreateAuditTrigger(migrationBuilder, "symptoms", "Symptom", "symptom_id");
            CreateAuditTrigger(migrationBuilder, "ibs_sss_assessments", "IbsSssAssessment", "assessment_id");
            CreateAuditTrigger(migrationBuilder, "patient_profiles", "PatientProfile", "profile_id");
            CreateAuditTrigger(migrationBuilder, "patient_allergies", "PatientAllergy", "patient_allergy_id");
            CreateAuditTrigger(migrationBuilder, "nutritionist_patient", "NutritionistPatient", "assignment_id");
            CreateAuditTrigger(migrationBuilder, "recommendation_feedback", "RecommendationFeedback", "feedback_id");
        }

        private static void CreateAuditTrigger(
            MigrationBuilder migrationBuilder,
            string table,
            string entityType,
            string primaryKeyColumn)
        {
            migrationBuilder.Sql($@"
                CREATE TRIGGER tr_{table}_audit
                    AFTER INSERT OR UPDATE OR DELETE ON {table}
                    FOR EACH ROW EXECUTE FUNCTION audit_trigger_fn('{entityType}', '{primaryKeyColumn}');");
        }

        private static void DropAuditTrigger(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.Sql($"DROP TRIGGER IF EXISTS tr_{table}_audit ON {table};");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropAuditTrigger(migrationBuilder, "recommendation_feedback");
            DropAuditTrigger(migrationBuilder, "nutritionist_patient");
            DropAuditTrigger(migrationBuilder, "patient_allergies");
            DropAuditTrigger(migrationBuilder, "patient_profiles");
            DropAuditTrigger(migrationBuilder, "ibs_sss_assessments");
            DropAuditTrigger(migrationBuilder, "symptoms");
            DropAuditTrigger(migrationBuilder, "meals");
            DropAuditTrigger(migrationBuilder, "recommendations");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS audit_trigger_fn();");

            migrationBuilder.DropTable(
                name: "clinical_reports_metadata");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "fcm_token",
                table: "users");
        }
    }
}
