# Acta A51: Mecanismo de activación de cuentas de nutricionista

**Estado:** Aprobada, RESUELTA en Nutritionist-Activation-1
**Fecha:** 2026-09-11
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, dominio `User`, `LoginCommandHandler` y pipeline de MediatR.

---

## Contexto

El acta [A47](A47-nutritionist-deferred-activation-debt.md) registró que el nutricionista nacía en
`UserStatus.Active` y que ninguna transición de estado tenía llamador en la aplicación. Por eso las ramas
de `nutritionist_not_available` eran inalcanzables.

El diseño inicial proponía activar al nutricionista solo dentro de `LoginCommandHandler`. En la Fase 0
apareció un problema: el cliente `cauce-web-portal` tiene Direct Access Grants apagado, lo que apunta a que
el portal usará Authorization Code + PKCE directo contra Keycloak, sin pasar por `POST /auth/login`. Con ese
diseño, ningún nutricionista del portal se habría activado nunca. La decisión se corrigió antes de
implementar.

## Decisión

1. **Estado inicial en el dominio.** `User.CreateNutritionist` crea la cuenta en `PendingActivation`, con
   el correo verificado. La regla vive en la fábrica, no solo en el handler: el dominio deja de afirmar que
   el nutricionista nace activo.
2. **Una sola regla.** `INutritionistActivationService.ActivateIfPendingAsync` activa únicamente a un
   nutricionista pendiente con el correo verificado. Audita la transición con `AuditActionType.AccountActivation`,
   con el propio nutricionista como actor y el punto de entrada en el contexto (`login` o
   `authenticated_request`). No confirma: eso le toca al llamador.
3. **Dos puntos de entrada.**
   - **Login.** `LoginCommandHandler` invoca la regla porque en esa petición la identidad recién aparece
     cuando Keycloak responde. La confirma su propio `SaveChanges`, en la misma transacción que
     `last_login_at`, igual que la sincronización del acta A39.
   - **Cualquier otra petición autenticada.** `NutritionistActivationBehavior` descarta por el rol del
     token sin consultar la base, y **confirma por su cuenta**, porque las consultas no llaman a
     `SaveChanges` y la activación se perdería. Va registrado entre `ValidationBehavior` y
     `AuditingBehavior`: si fuera después, su `SaveChanges` persistiría antes de tiempo la fila de intención
     de un comando cuyo handler todavía puede fallar (acta A8).

## Por qué autenticarse prueba la activación

Desde el acta [A52](A52-keycloak-activation-link-and-resend.md), el nutricionista nace **sin contraseña**,
y Keycloak solo emite un token cuando las acciones requeridas están resueltas. Verificado contra Keycloak
25.0.6: con `UPDATE_PASSWORD` pendiente, Direct Grant responde 400 `Account is not fully set up`.

**Caso borde verificado:** si un administrador retira `UPDATE_PASSWORD` desde la consola sin cambiar la
contraseña, el login entra igual. No aplica a cuentas provisionadas, porque no tienen contraseña temporal
que destrabar. `DevAdminSeeder` sí usa una temporal fija, y por eso activa su cuenta explícitamente.

## Consecuencias

- Cada petición de un nutricionista suma una lectura de `users`. Es aceptable para n=20–50.
- Si las dos primeras peticiones llegan en paralelo, en el peor caso quedan dos filas de auditoría. El
  estado final es el mismo.
- `DevAdminSeeder` activa al nutricionista demo, porque `DemoPatientSeeder` solo le asigna el paciente demo
  a uno activo.
- Pruebas migradas según el acta A45: los helpers de siembra activan explícitamente y `UserTests` afirma
  `PendingActivation`.
- No hay migración: `audit_logs.action_type` es `varchar(50)` y no tiene CHECK.

## Referencias

- `src/Cauce.Domain/Identity/User.cs`
- `src/Cauce.Application/Identity/Services/NutritionistActivationService.cs`
- `src/Cauce.Application/Common/Behaviors/NutritionistActivationBehavior.cs`
- `src/Cauce.Application/Identity/UseCases/Login/LoginCommandHandler.cs`
- Actas [A47](A47-nutritionist-deferred-activation-debt.md), [A52](A52-keycloak-activation-link-and-resend.md) y
  A39.
