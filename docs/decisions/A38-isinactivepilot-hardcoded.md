# Acta A38: Deuda diferida — isInActivePilot hardcodeado

**Estado:** Aprobada, deuda diferida. Bloqueada por dependencia externa
**Fecha:** 2026-09-08
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, flujo de registro de paciente.

---

## Contexto

El flag `isInActivePilot` determina si un paciente forma parte del piloto clínico. Tiene dos efectos
reales: viaja al cliente en el objeto `user` del login, y gobierna la retención de datos en la baja de
cuenta (US26), donde un paciente inscrito en un piloto activo no puede eliminar sus datos sin acuse
explícito.

Hoy el valor no se deriva de ninguna fuente autorizada. El Complejo Hospitalario Guillermo Kaelín de la
Fuente todavía no entregó la lista definitiva de pacientes que participarán del piloto, y sin esa lista
no hay contra qué verificar.

## Decisión

**No resolver en Backend-Fix-2.** Esperar la entrega de la lista por parte del hospital. Cuando esté
disponible, implementar la verificación real contra la tabla de pacientes autorizados.

## Consecuencias

- Durante el desarrollo, todos los pacientes registrados aparecen como parte del piloto activo. Es
  aceptable porque el ambiente es de desarrollo y ningún dato es clínico real.
- El gate de retención de US26 responde hoy de forma uniforme para todos, en vez de distinguir a los
  participantes reales.
- **Un smoke test previo al piloto real debe verificar que la lógica no se activó sin la lista
  completa.** Es el riesgo que hay que vigilar: activar la verificación con una lista parcial dejaría
  fuera del piloto a pacientes que sí participan.

## Bloqueante

Dependencia externa del Complejo Hospitalario Guillermo Kaelín de la Fuente (EsSalud Lima Sur). No es
trabajo técnico pendiente: es información que el backend no puede producir por su cuenta.

## Otra deuda diferida de este bloque

Backend-Fix-2 identificó una segunda deuda que también se difiere, registrada en el acta
[A47](A47-nutritionist-deferred-activation-debt.md): no existe ciclo de vida de cuentas de nutricionista.
A diferencia de esta, **A47 no tiene bloqueante externo** y ya tiene bloque asignado,
Nutritionist-Activation-1.

## Referencias

- `src/Cauce.Application/Identity/UseCases/RegisterPatient/RegisterPatientCommandHandler.cs`
- `src/Cauce.Domain/Identity/User.cs` (`EnrollInActivePilot`).
- Acta A16, activación manual de `IsInActivePilot` por el Kaelín.
- Acta [A47](A47-nutritionist-deferred-activation-debt.md), la otra deuda diferida del bloque.
