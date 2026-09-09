# Acta A41: Endpoint de canje de código de invitación post-registro

**Estado:** Aprobada, RESUELTA en Backend-Fix-2 (tag `v0.8.0-backend-fix-2`)
**Fecha:** 2026-09-08
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, endpoint autenticado nuevo más refactor de dos handlers existentes.

---

## Contexto

Un paciente que se registraba sin código de invitación no tenía forma de vincularse a un nutricionista
después. El código solo se aceptaba en `POST /auth/register`, así que el vínculo únicamente podía nacer
durante el registro inicial. Era un callejón sin salida operativo: la cuenta quedaba sin nutricionista y
sin ningún camino de recuperación dentro de la aplicación.

`MATRIZ-IDENTIDAD.md` lo registraba como **US20-CA02 en estado PARCIAL**, con la evidencia literal
«Canje posterior: NO EXISTE».

## Decisión

Nuevo endpoint `POST /api/v1/patients/me/nutritionist-assignment`, autenticado con `Policy=Patient`,
precedido de un refactor que extrae la lógica de vinculación a un servicio dedicado.

## Decisiones complementarias

- **D5.** Paciente ya asignado → 409 `patient_already_assigned`. Sin sobrescritura: cambiar de
  nutricionista es una decisión clínica, no el efecto de que el paciente pegue otro código.
- **D6 reemplazada por A43.** La premisa de D6 era falsa: la notificación al nutricionista ya existía y
  el handler de registro ya la emitía. Se reusa el evento de dominio existente, con el texto
  parametrizado por contexto, en vez de agendar una notificación nueva que habría producido dos correos
  por un solo canje.
- **D7.** Auditoría del canje en `audit_logs`.
- **D8.** Refactor primero, endpoint después.
- **D11.** Nutricionista no disponible → 409, y **el código no se consume**.
- **R10.** Validación exhaustiva, con las mismas reglas de formato que aplica el registro.

## La vinculación estaba partida en dos handlers

El hallazgo que reescribió el alcance del refactor. La lógica no vivía toda en
`RegisterPatientCommandHandler`, como se suponía:

| Paso | Dónde ocurría |
| --- | --- |
| Consumir el código y publicar el evento de vinculación | `RegisterPatientCommandHandler` |
| **Crear la fila de `nutritionist_patient`** | `CreatePatientProfileCommandHandler` |

El registro **no creaba el vínculo real**: esa fila nacía recién al crear el perfil clínico. Por eso
`IPatientNutritionistAssignmentService` expone **dos operaciones** y no una. Fundirlas habría obligado al
registro a crear la asignación antes de que existiera el perfil, un cambio de comportamiento observable.

De haberse implementado el endpoint con una sola operación, el canje habría marcado el código como
consumido **sin asignar realmente al paciente**.

## Nueva validación (D11)

El nutricionista dueño del código debe estar en `UserStatus.Active` al momento del canje. Cualquier otro
estado falla con **409 `nutritionist_not_available`** y **el código no se consume**, de modo que pueda
reemitirse o reutilizarse.

Se usa un único `errorCode` para el escenario, con el estado exacto en la extensión `reason` del
envelope: `pending_activation`, `inactive` o `suspended`. Para el cliente es un solo caso arquitectónico,
«el nutricionista no puede atender», y el detalle le permite afinar el mensaje o ignorarlo.

**La protección contra `UserStatus.PendingActivation` implementada en este endpoint quedará
operativamente activa cuando se ejecute el bloque Nutritionist-Activation-1 (ver acta
[A47](A47-nutritionist-deferred-activation-debt.md)).** Hoy ninguna transición de estado de cuentas de
nutricionista tiene invocación en la aplicación, así que la comprobación es defensiva y está probada,
pero no alcanzable por vía de la aplicación.

## Consecuencias

- Refactor de dos handlers con comportamiento externo idéntico, verificado porque las pruebas de
  integración de registro y de creación de perfil pasaron sin modificarse.
- Servicio nuevo `IPatientNutritionistAssignmentService`, con dos sobrecargas de establecimiento: una
  que resuelve el código por paciente y otra que lo recibe ya resuelto, necesaria porque en el canje el
  `MarkAsUsed` todavía no está persistido cuando hay que crear el vínculo.
- Dos `errorCode` nuevos: `patient_already_assigned` (409) y `nutritionist_not_available` (409), este
  último con la extensión `reason`.
- La auditoría del canje se registra **sobre `invitation_codes`**, no sobre `nutritionist_patient`: esa
  tabla ya tiene trigger de auditoría (DEC-B5-03) y duplicarla habría violado la regla de no duplicación
  del acta A8. Se auditan tanto el canje efectivo como el rechazado, con el estado exacto del
  nutricionista en el contexto.
- El refactor obligó a migrar pruebas existentes, bajo el marco del acta A45.

## Referencias

- `src/Cauce.Api/Controllers/PatientsController.cs`
- `src/Cauce.Application/Patients/UseCases/AssignNutritionist/`
- `src/Cauce.Application/Patients/Services/PatientNutritionistAssignmentService.cs`
- `src/Cauce.Application/Common/Interfaces/Patients/IPatientNutritionistAssignmentService.cs`
- Acta [A43](A43-domain-event-for-nutritionist-notification.md), que reemplaza a D6.
- Acta [A45](A45-test-migration-on-constructor-refactor.md), migración de pruebas en el refactor.
- Acta [A47](A47-nutritionist-deferred-activation-debt.md), ciclo de vida de cuentas de nutricionista.
- `docs/api/CONTRACT-IDENTITY-v1.md` v1.2, §2.11.
