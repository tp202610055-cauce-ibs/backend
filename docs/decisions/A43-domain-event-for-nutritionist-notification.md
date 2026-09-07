# Acta A43: Reemplazo de D6, uso del evento de dominio existente para notificar al nutricionista, con parametrización del texto por contexto

**Estado:** Aprobada, aplicada en Backend-Fix-2 Fase 3
**Fecha:** 2026-09-06
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, notificación al nutricionista ante la vinculación de un paciente. No cambia el contrato de API.

---

## Contexto

La decisión **D6** del bloque Backend-Fix-2 indicaba usar «el `INotificationService` existente» para
avisar al nutricionista del canje post-registro, y encargaba a la Fase 0 verificar qué canales soporta,
asumiendo la existencia de un canal in-app.

La verificación de Fase 0 encontró que **la premisa era incorrecta en tres puntos**:

| D6 asumía | El código tiene |
| --- | --- |
| Un servicio `INotificationService` | `INotificationScheduler`, `INotificationSender` e `INotificationRepository` |
| Un canal in-app | `NotificationChannel` solo declara `Push` y `Email` |
| Que la notificación había que construirla | **Ya está construida** |

`PatientLinkedToNutritionistEventHandler` escucha `PatientLinkedToNutritionistEvent` y agenda un correo
al nutricionista (`NotificationType.Alert`, `NotificationChannel.Email`), con idempotencia garantizada
por `ExistsForRelatedAsync` usando el identificador del código de invitación como clave. Es el
cumplimiento de US20 CA01, entregado en el Bloque 6.

### El riesgo concreto que se evitó

El prompt contenía además una contradicción interna en la Fase 3. Su línea 751 dice que «si el handler
actual NO envía notificación al nutricionista, el servicio extraído TAMPOCO la envía», y la 752 manda
agregarla en la Fase 4. La premisa es falsa: `RegisterPatientCommandHandler` **sí** notifica, publicando
el evento por outbox dentro del mismo bloque que la Fase 3 extrae.

De haberse seguido al pie de la letra, el servicio extraído se habría llevado la publicación del evento
**y** la Fase 4 habría agendado una notificación propia: **dos correos al nutricionista por un solo
canje**.

## Decisión

**D6 queda reemplazada por esta acta.**

1. El servicio extraído en la Fase 3 **conserva la emisión** de `PatientLinkedToNutritionistEvent`. El
   endpoint de canje post-registro hereda la notificación por outbox, sin escribir una sola línea de
   código de notificaciones.
2. Se agrega el parámetro `Context`, de tipo `LinkContext`, al evento, para que el handler redacte el
   texto correcto en cada flujo.

`LinkContext` tiene dos valores: `RegistrationLink` y `PostRegistrationLink`.

### Mitigación del riesgo del parámetro nuevo

`Context` es el quinto parámetro posicional del `record` y **tiene `RegistrationLink` como valor por
defecto**. Eso preserva tres cosas a la vez:

- El flujo de registro se comporta igual: `RegisterPatientCommandHandler` omite el parámetro.
- Toda construcción de cuatro argumentos sigue compilando, incluida la de las pruebas existentes.
- Los eventos ya persistidos en el outbox deserializan con el valor por defecto, sin migración.

El texto de `RegistrationLink` es **idéntico carácter por carácter** al que había. El de
`PostRegistrationLink` distingue que el paciente ya tenía cuenta y canjeó el código después, porque
decirle a un nutricionista que el paciente «se registró con tu código» cuando en realidad se registró
sin código sería inexacto, y esos correos los leen personas durante el piloto.

## Consecuencias

- Cero código nuevo de notificaciones. Un solo correo por canje.
- Idempotencia ya resuelta por la clave del código de invitación, que es única por canje y sirve igual
  a los dos flujos.
- Consistencia: registro y canje producen el mismo hecho de dominio, tratado por el mismo handler.
- No se necesita decidir ningún canal de reemplazo: el proyecto ya había decidido Email para esta
  notificación en US20 CA01.
- La Fase 4 pierde su tarea 8 (notificación explícita) y conserva la 7 (auditoría
  `NutritionistAssignment`), que sí es específica del canje.

## Referencias

- `src/Cauce.Domain/Identity/Enums/LinkContext.cs`
- `src/Cauce.Domain/Identity/Events/PatientLinkedToNutritionistEvent.cs`
- `src/Cauce.Application/Identity/EventHandlers/PatientLinkedToNutritionistEventHandler.cs`
- `src/Cauce.Application/Patients/Services/PatientNutritionistAssignmentService.cs`
- Acta A41, endpoint de canje de código de invitación post-registro.
- Decisión D6 del prompt Backend-Fix-2, reemplazada por esta acta.
