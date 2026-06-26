using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cauce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuditTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    audit_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    old_values_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    new_values_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    additional_context = table.Column<string>(type: "jsonb", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now() at time zone 'utc'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.audit_log_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_action_type_occurred_at",
                table: "audit_logs",
                columns: new[] { "action_type", "occurred_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor_user_id_occurred_at",
                table: "audit_logs",
                columns: new[] { "actor_user_id", "occurred_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity_type_entity_id",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id" },
                filter: "entity_id IS NOT NULL");

            // Función e inmutabilidad de audit_logs (DEC-B3-09): ninguna operación
            // UPDATE o DELETE puede ejecutarse sobre la tabla, ni siquiera con
            // acceso directo a la base de datos. Garantiza el no repudio exigido
            // por la Ley N° 29733.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION reject_audit_log_modification()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_logs is immutable: % operations are not allowed on this table',
                        TG_OP USING ERRCODE = 'check_violation';
                END;
                $$ LANGUAGE plpgsql;");

            migrationBuilder.Sql(@"
                CREATE TRIGGER tr_audit_logs_reject_update
                    BEFORE UPDATE ON audit_logs
                    FOR EACH ROW EXECUTE FUNCTION reject_audit_log_modification();");

            migrationBuilder.Sql(@"
                CREATE TRIGGER tr_audit_logs_reject_delete
                    BEFORE DELETE ON audit_logs
                    FOR EACH ROW EXECUTE FUNCTION reject_audit_log_modification();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_audit_logs_reject_delete ON audit_logs;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_audit_logs_reject_update ON audit_logs;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS reject_audit_log_modification();");

            migrationBuilder.DropTable(
                name: "audit_logs");
        }
    }
}
