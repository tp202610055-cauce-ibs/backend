# Acta A53: Estado del nutricionista al registrarse con un código y al generarlo

**Estado:** Aprobada, RESUELTA en Nutritionist-Activation-1
**Fecha:** 2026-09-11
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, `POST /api/v1/auth/register` y `POST /api/v1/invitations`.

---

## Contexto

La decisión D11 del acta [A41](A41-nutritionist-assignment-endpoint.md) exige que el nutricionista dueño de
un código esté activo, pero solo se aplicaba en el canje posterior al registro. La Fase 0 encontró que el
canje dentro de `POST /auth/register` no validaba el estado del nutricionista, y que la generación de
códigos tampoco lo hacía.

## Decisión

- **Registro.** Después de comprobar que el código está vigente, se exige que su nutricionista esté
  `Active`. Si no lo está, responde **409 `nutritionist_not_available`** con la extensión `reason`. El
  rechazo ocurre **antes de crear el usuario en Keycloak**, así que no hay nada que compensar y el código no
  se consume. Un código que apunta a un nutricionista inexistente se trata como
  `invalid_invitation_code`, con un log crítico. No se audita, igual que el resto de los rechazos del
  registro.
- **Generación.** Después de comprobar el rol, se exige `Active`, con el mismo 409 y su `reason`.
  - Un nutricionista pendiente ya llega activado por el behavior (acta [A51](A51-nutritionist-activation-mechanism.md)),
    así que para él este chequeo es solo un respaldo.
  - La protección real es contra una cuenta suspendida o dada de baja que todavía tiene un token vigente.
- El mensaje de `NutritionistNotAvailableException` se generalizó para cubrir los tres puntos donde se
  lanza.

## Consecuencias

- `CONTRACT-IDENTITY` pasa a v1.3: el registro puede responder un 409 nuevo. **El cliente móvil todavía no
  mapea este `errorCode`.**
- Con el acta A51, un nutricionista pendiente no puede tener códigos por ninguna vía de la aplicación, así
  que la rama `pending_activation` queda como defensa.
- Las ramas `suspended` e `inactive` se alcanzarán cuando exista el flujo de suspensión (actas
  [A55](A55-suspension-session-enforcement-deferred.md) y [A56](A56-invitation-revocation-on-suspension-deferred.md)).
  Hoy están cubiertas por pruebas de integración que siembran el estado directamente.

## Referencias

- `src/Cauce.Application/Identity/UseCases/RegisterPatient/RegisterPatientCommandHandler.cs`
- `src/Cauce.Application/Identity/UseCases/GenerateInvitationCode/GenerateInvitationCodeCommandHandler.cs`
- `src/Cauce.Domain/Patients/Exceptions/NutritionistNotAvailableException.cs`
- `tests/Cauce.Api.IntegrationTests/Identity/InvitationNutritionistStatusTests.cs`
