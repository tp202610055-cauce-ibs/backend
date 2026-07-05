# Outbox (Prompt 5, DEC-B5-04)

Patrón outbox transaccional para publicar eventos de dominio de forma confiable.

- `OutboxWriter` (`IOutboxWriter`): serializa el evento (`OutboxSerialization.Options`, enums como
  texto) y lo enrola como `OutboxMessage` en el `ChangeTracker`. **No** llama `SaveChanges`: el
  `UnitOfWork` del handler cierra la transacción, garantizando que el evento se persista atómicamente
  con el cambio de negocio.
- `OutboxRepository` (`IOutboxRepository`): lista pendientes (índice parcial `WHERE processed_at IS NULL`),
  obtiene por id (con tracking) y purga procesados antiguos.
- `OutboxEventTypeRegistry`: mapea `event_type` (nombre completo del tipo) → `Type` escaneando el
  ensamblado de dominio, y arma `DomainEventNotification<TEvent>` para el dispatcher.

El `OutboxDispatcherWorker` (Api) consume la cola. Los `INotificationHandler<DomainEventNotification<TEvent>>`
de Application reaccionan (notificar, loguear), garantizando exactly-once por verificación de existencia.
