# Notifications (Prompt 5, DEC-B5-05)

Entrega asíncrona de notificaciones a los usuarios por canal (push / email).

- `NotificationRepository` / `NotificationScheduler`: enrolan y listan notificaciones. `ExistsForRelatedAsync`
  respalda el efecto exactly-once ante el re-procesamiento del outbox (junto al índice único filtrado
  `ux_notifications_dedup`).
- `Senders/`:
  - `SmtpEmailNotificationSender` (canal `Email`): resuelve el correo del destinatario y envía vía
    `SmtpMessageDispatcher` (MailKit).
  - `FirebaseCloudMessagingSender` (canal `Push`): resuelve `users.fcm_token` y envía vía FCM.
  - `FakeFcmSender` (canal `Push`): sustituto en dev/pruebas (`Notifications:Fcm:UseFake=true`); no hace red.

El `NotificationDispatcherWorker` (Api) toma las pendientes vencidas en orden `scheduled_for`, invoca al
sender del canal y aplica la máquina de estados de `Notification` (Sent / backoff / Failed).

**Deuda técnica:** falta `PUT /users/me/fcm-token` para que la app registre su token; sin él el push
real no es alcanzable (acta A2).
