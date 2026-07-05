# Workers (Prompt 5)

`BackgroundService` de larga vida. Cada uno resuelve servicios *scoped* por tick vía
`IServiceScopeFactory` y se puede deshabilitar con `Workers:{Name}:Enabled=false`
(útil en pruebas de integración).

| Worker | Cadencia | Responsabilidad |
| --- | --- | --- |
| `OutboxDispatcherWorker` | 5 s | Publica los mensajes pendientes de `outbox_messages` como `DomainEventNotification<TEvent>`. Cada mensaje en su propio scope + `SaveChanges` (aísla fallos; envenenado a los 10 intentos). |
| `NotificationDispatcherWorker` | 10 s | Envía las notificaciones pendientes por su canal (push/email), en orden `scheduled_for`. Aplica backoff exponencial ante fallos. |
| `RecommendationExpirationWorker` | 6 h | Transita a `Expired` las recomendaciones vencidas en estado expirable (barrido de zombies). |
| `OutboxRetentionWorker` | 24 h | Elimina mensajes de outbox procesados con más de 30 días. |
| `WeeklyRecommendationReminderWorker` | Lunes 09:00 Lima | Agenda un recordatorio push para cada paciente activo. Calcula el próximo tick con `TimeZoneInfo` (UTC→Lima). |

**Nota de escalado (deuda técnica):** asumen una sola instancia. Para escalar horizontalmente,
añadir `SELECT ... FOR UPDATE SKIP LOCKED` en las lecturas de outbox/notifications.
