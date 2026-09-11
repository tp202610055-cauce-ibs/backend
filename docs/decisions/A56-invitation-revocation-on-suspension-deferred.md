# Acta A56: Deuda diferida — los códigos de invitación no se revocan al suspender

**Estado:** Aprobada, deuda diferida. Se resuelve junto con el flujo de suspensión
**Fecha:** 2026-09-11
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, entidad `InvitationCode`.

---

## Contexto

`InvitationCode` solo expira por tiempo (72 horas) y no tiene forma de revocarse. Con el acta
[A53](A53-nutritionist-status-in-invitation-codes.md), los canjes de códigos de un nutricionista suspendido
se rechazan con 409, así que el daño práctico queda acotado. Aun así, quedan dos efectos:

- El código sigue en estado `Active`.
- Si el nutricionista se reactiva dentro de las 72 horas, sus códigos vuelven a funcionar sin que nadie
  los haya reemitido.

## Decisión

No implementar en este bloque. Opciones para cuando exista el flujo de suspensión:

1. Agregar un método de dominio `Revoke` con un estado `Revoked`, y revocar en cascada al suspender.
2. Aceptar el comportamiento actual, ya que el rechazo de A53 más la vigencia de 72 horas pueden alcanzar.

## Consecuencias

El riesgo es bajo por la vigencia corta de los códigos y el rechazo en el canje. Las asignaciones activas
de un nutricionista suspendido tampoco se tocan: esa es una decisión clínica que le corresponde al bloque
que defina la suspensión.

## Referencias

- `src/Cauce.Domain/Identity/InvitationCode.cs`
- Actas [A47](A47-nutritionist-deferred-activation-debt.md) y [A55](A55-suspension-session-enforcement-deferred.md).
