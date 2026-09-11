# Acta A55: Deuda diferida — la suspensión no corta sesiones ni tokens ya emitidos

**Estado:** Aprobada, deuda diferida. Se resuelve junto con el flujo de suspensión
**Fecha:** 2026-09-11
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, autorización de peticiones y ciclo de vida de cuentas.

---

## Contexto

Ni el login ni la autorización revisan `User.Status` una vez que el token fue emitido. `User.Suspend()`
sigue sin llamador en la aplicación (acta [A47](A47-nutritionist-deferred-activation-debt.md)). Si el día
de mañana se suspende una cuenta cambiando solo el estado local:

- El JWT sigue siendo válido hasta 15 minutos.
- La renovación sigue funcionando mientras Keycloak no deshabilite al usuario.
- El login por Direct Grant sigue entrando.

## Decisión

No implementar en este bloque. El bloque que exponga la suspensión debería:

1. Llamar a `IKeycloakAdminClient.DisableUserAsync` al suspender. Ya existe y la usa la baja de pacientes
   (US26). Con eso se cortan el login y la renovación.
2. Revocar las sesiones abiertas del usuario en Keycloak, si la ventana de un refresh token vigente no es
   aceptable.
3. Revisar el estado en el pipeline para cubrir la ventana del access token. El
   `NutritionistActivationBehavior` (acta [A51](A51-nutritionist-activation-mechanism.md)) ya lee al
   nutricionista en cada petición, así que es el lugar natural.

## Consecuencias

Para el caso concreto de generar códigos, el chequeo del acta
[A53](A53-nutritionist-status-in-invitation-codes.md) ya cubre esa ventana: un nutricionista suspendido con
un token vigente no puede emitirlos.

## Referencias

- `src/Cauce.Domain/Identity/User.cs` (`Suspend`, `Reactivate`)
- `src/Cauce.Infrastructure/Identity/KeycloakAdminClient.cs` (`DisableUserAsync`)
