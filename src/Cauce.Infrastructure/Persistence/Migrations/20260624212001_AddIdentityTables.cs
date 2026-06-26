using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cauce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    role_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => x.role_id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    keycloak_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    full_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email_verified = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_login_attempts = table.Column<int>(type: "integer", nullable: false),
                    locked_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_users_user_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "user_roles",
                        principalColumn: "role_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "consent_records",
                columns: table => new
                {
                    consent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    consent_text_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consent_records", x => x.consent_id);
                    table.ForeignKey(
                        name: "fk_consent_records_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invitation_codes",
                columns: table => new
                {
                    code_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nutritionist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    used_by_patient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invitation_codes", x => x.code_id);
                    table.ForeignKey(
                        name: "fk_invitation_codes_users_nutritionist_id",
                        column: x => x.nutritionist_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_invitation_codes_users_used_by_patient_id",
                        column: x => x.used_by_patient_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_used = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_password_reset_tokens", x => x.token_id);
                    table.ForeignKey(
                        name: "fk_password_reset_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_consent_records_user_id",
                table: "consent_records",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitation_codes_nutritionist_id",
                table: "invitation_codes",
                column: "nutritionist_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitation_codes_status_expires_at",
                table: "invitation_codes",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_invitation_codes_used_by_patient_id",
                table: "invitation_codes",
                column: "used_by_patient_id");

            migrationBuilder.CreateIndex(
                name: "ux_invitation_codes_code",
                table: "invitation_codes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_user_id",
                table: "password_reset_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_password_reset_tokens_token_hash",
                table: "password_reset_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_user_roles_role_name",
                table: "user_roles",
                column: "role_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_role_id",
                table: "users",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_status",
                table: "users",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_users_keycloak_id",
                table: "users",
                column: "keycloak_id",
                unique: true);

            // Inmutabilidad de consent_records (Ley N° 29733): solo se permite
            // modificar el campo is_current; cualquier otro cambio o eliminación se
            // bloquea a nivel de base de datos.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION fn_consent_records_prevent_update()
                RETURNS trigger AS $$
                BEGIN
                    IF (OLD.consent_id IS DISTINCT FROM NEW.consent_id
                        OR OLD.user_id IS DISTINCT FROM NEW.user_id
                        OR OLD.document_version IS DISTINCT FROM NEW.document_version
                        OR OLD.accepted_at IS DISTINCT FROM NEW.accepted_at
                        OR OLD.ip_address IS DISTINCT FROM NEW.ip_address
                        OR OLD.consent_text_hash IS DISTINCT FROM NEW.consent_text_hash) THEN
                        RAISE EXCEPTION 'Los registros de consent_records son inmutables excepto el campo is_current.';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;");

            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_consent_records_prevent_update
                BEFORE UPDATE ON consent_records
                FOR EACH ROW EXECUTE FUNCTION fn_consent_records_prevent_update();");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION fn_consent_records_prevent_delete()
                RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'Los registros de consent_records no pueden eliminarse.';
                END;
                $$ LANGUAGE plpgsql;");

            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_consent_records_prevent_delete
                BEFORE DELETE ON consent_records
                FOR EACH ROW EXECUTE FUNCTION fn_consent_records_prevent_delete();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_consent_records_prevent_delete ON consent_records;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_consent_records_prevent_update ON consent_records;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_consent_records_prevent_delete();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_consent_records_prevent_update();");

            migrationBuilder.DropTable(
                name: "consent_records");

            migrationBuilder.DropTable(
                name: "invitation_codes");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "user_roles");
        }
    }
}
