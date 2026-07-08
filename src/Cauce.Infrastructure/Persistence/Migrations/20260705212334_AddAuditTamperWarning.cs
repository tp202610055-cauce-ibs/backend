using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cauce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditTamperWarning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TS04 CA02: además de bloquear la modificación (RAISE EXCEPTION), se emite un RAISE WARNING
            // con el actor (GUC cauce.actor_user_id), la operación (TG_OP) y el momento. El WARNING se
            // escribe al server log fuera de la transacción abortada, por lo que sí persiste, y permite
            // una bitácora separada de intentos de manipulación de audit_logs (acta A18).
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION reject_audit_log_modification()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE WARNING 'audit_tamper_attempt actor=% operation=% at=%',
                        current_setting('cauce.actor_user_id', true),
                        TG_OP,
                        (now() at time zone 'utc');
                    RAISE EXCEPTION 'audit_logs is immutable: % operations are not allowed on this table',
                        TG_OP USING ERRCODE = 'check_violation';
                END;
                $$ LANGUAGE plpgsql;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restaura la función original sin el RAISE WARNING (estado de InitialAuditTrigger).
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION reject_audit_log_modification()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_logs is immutable: % operations are not allowed on this table',
                        TG_OP USING ERRCODE = 'check_violation';
                END;
                $$ LANGUAGE plpgsql;");
        }
    }
}
