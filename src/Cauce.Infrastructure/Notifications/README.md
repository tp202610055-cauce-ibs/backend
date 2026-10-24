# Notifications (Prompt 5, DEC-B5-05)

Entrega asíncrona de notificaciones a los usuarios por canal (push / email).

- `NotificationRepository` / `NotificationScheduler`: enrolan y listan notificaciones. `ExistsForRelatedAsync`
  respalda el efecto exactly-once ante el re-procesamiento del outbox (junto al índice único filtrado
  `ux_notifications_dedup`, por usuario, tipo, tipo de entidad e id de entidad).
- `Senders/`:
  - `SmtpEmailNotificationSender` (canal `Email`): resuelve el correo del destinatario y envía vía
    `SmtpMessageDispatcher` (MailKit).
  - `FirebaseCloudMessagingSender` (canal `Push`): resuelve `users.fcm_token` y envía vía FCM.
  - `FirebaseMessagingProvider`: crea el cliente de Firebase una sola vez, al arrancar la API.
  - `FakeFcmSender` (canal `Push`): sustituto en dev/pruebas (`Notifications:Fcm:UseFake=true`); no hace red.

El `NotificationDispatcherWorker` (Api) toma las pendientes vencidas en orden `scheduled_for`, invoca al
sender del canal y aplica la máquina de estados de `Notification` (Sent / backoff / Failed).

## Registro del token

La app registra su token con `PUT /api/v1/users/me/fcm-token` (máximo 500 caracteres; `null` lo desvincula).
Un teléfono tiene un solo token: si otra cuenta ya lo tenía, se le quita en la misma transacción. La
auditoría registra el cambio sin el token.

## Mensaje push

| Parte | Contenido |
| --- | --- |
| `notification` | `title` siempre `Cauce` y `body` de `PatientPushTexts`. Lo muestra el sistema aunque la app esté cerrada |
| `data` | Todas las claves son texto: `type` (el `NotificationType` en minúsculas), `entityType` y `entityId` (vacíos si no hay entidad) |
| `android` | Prioridad alta y canal `cauce_default`, que crea la app |

Los textos se ven en la pantalla de bloqueo: no nombran el cuestionario clínico, síntomas, diagnósticos ni
al nutricionista.

| Cuándo | `type` | `entityType` | `entityId` | Cuerpo |
| --- | --- | --- | --- | --- |
| Recomendación aprobada, modificada o creada a mano | `recommendation` | `recommendation` | id de la recomendación | «Tienes una recomendación nueva. Toca para verla.» |
| 24 horas después de entregada una recomendación | `reminder` | `recommendation` | id de la recomendación | «¿Cómo te fue con la recomendación? Cuéntanos si la aplicaste y cómo te sentiste.» |
| Día del vencimiento del cuestionario (fecha de Lima) | `info` | `ibs_sss_schedule` | id de la agenda | «Ya puedes completar tu cuestionario de seguimiento. Toca para empezar.» |
| 48 horas después del vencimiento, si sigue pendiente | `reminder` | `ibs_sss_schedule` | id de la agenda | «Tienes un cuestionario de seguimiento pendiente. Toca para completarlo.» |
| Recordatorio semanal (lunes 09:00 de Lima) | `reminder` | vacío | vacío | «Recuerda registrar tu diario esta semana.» |

Mientras una recomendación está en revisión el paciente no recibe push. El rechazo tampoco genera uno.

## Fallos

- Sin token registrado, el envío falla y sigue el backoff (1, 5 y 30 minutos; falla al tercer intento).
- FCM `Unregistered`, o `InvalidArgument` que habla del token de registro: el token se desvincula del usuario.
  Cualquier otro error, incluido un mensaje mal formado, es un fallo de envío con reintento normal.
- Con `UseFake=false`, si la credencial de Firebase falta o es inválida, la API arranca igual, registra el
  error `Firebase Cloud Messaging could not be initialized` y cada push falla con `fcm_unavailable` hasta
  corregir el archivo y reiniciar la API. El correo no se ve afectado.

## Agenda del cuestionario

`IbsSssScheduleProcessor` corre a las 09:00 de Lima (`IbsSssReminderWorker`) y a demanda con
`dotnet Cauce.Api.dll process-ibs-sss`, que imprime cuántos recordatorios, avisos y agendas perdidas procesó.
Cada agenda recibe como máximo un aviso del día y un recordatorio de 48 horas: si al procesarla ya pasaron
48 horas, solo recibe el recordatorio.

Regla del aviso del día. `DueDate` es la medianoche UTC del día de vencimiento, que en Lima (UTC-5) son las
19:00 del día anterior. El aviso sale a las 09:00 de Lima del día de Lima en que cae `DueDate`, es decir,
14 horas antes de `DueDate`. El cuestionario ya se acepta desde 24 horas antes de `DueDate`
(`IbsSssAssessmentSchedule.EarlySubmissionTolerance`), así que el paciente puede completarlo apenas recibe
el aviso.

| `DueDate` (UTC) | Día de Lima | Aviso del día | Se acepta desde |
| --- | --- | --- | --- |
| 25-oct 00:00 | 24-oct (19:00) | 24-oct 09:00 Lima (14:00 UTC) | 24-oct 00:00 UTC (23-oct 19:00 Lima) |
