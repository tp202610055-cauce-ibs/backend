using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cauce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientCodeAndClinicalNoteClientGuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "patient_code_seq");

            migrationBuilder.AddColumn<string>(
                name: "patient_code",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "client_guid",
                table: "clinical_notes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Relleno de las filas existentes ANTES de crear los índices únicos: sin esto, una base con
            // pacientes previos quedaría con patient_code en NULL (sin código en exportaciones) y, si
            // algún paciente tuviera más de una nota, el índice de client_guid fallaría al crearse
            // porque todas las filas comparten el GUID cero que puso el valor por defecto.
            //
            // Los códigos se asignan por antigüedad de la cuenta para que el correlativo refleje el
            // orden real de alta. La secuencia se adelanta hasta el último valor usado, de modo que la
            // próxima alta continúe la numeración en lugar de chocar con lo ya asignado.
            migrationBuilder.Sql("""
                WITH numbered AS (
                    SELECT u.user_id,
                           ROW_NUMBER() OVER (ORDER BY u.created_at, u.user_id) AS correlative
                    FROM users u
                    JOIN user_roles r ON r.role_id = u.role_id
                    WHERE r.role_name = 'patient' AND u.patient_code IS NULL
                )
                UPDATE users
                SET patient_code = 'PAC-' || LPAD(numbered.correlative::text, 4, '0')
                FROM numbered
                WHERE users.user_id = numbered.user_id;
                """);

            migrationBuilder.Sql("""
                SELECT setval(
                    'patient_code_seq',
                    GREATEST((SELECT COUNT(*) FROM users WHERE patient_code IS NOT NULL), 1),
                    (SELECT COUNT(*) FROM users WHERE patient_code IS NOT NULL) > 0);
                """);

            migrationBuilder.Sql("""
                UPDATE clinical_notes
                SET client_guid = gen_random_uuid()
                WHERE client_guid = '00000000-0000-0000-0000-000000000000';
                """);

            migrationBuilder.CreateIndex(
                name: "ux_users_patient_code",
                table: "users",
                column: "patient_code",
                unique: true,
                filter: "patient_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_clinical_notes_patient_client_guid",
                table: "clinical_notes",
                columns: new[] { "patient_id", "client_guid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_users_patient_code",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ux_clinical_notes_patient_client_guid",
                table: "clinical_notes");

            migrationBuilder.DropColumn(
                name: "patient_code",
                table: "users");

            migrationBuilder.DropColumn(
                name: "client_guid",
                table: "clinical_notes");

            migrationBuilder.DropSequence(
                name: "patient_code_seq");
        }
    }
}
