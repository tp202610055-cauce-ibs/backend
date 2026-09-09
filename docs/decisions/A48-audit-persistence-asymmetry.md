# Acta A48: Asimetría en la persistencia de la auditoría de intentos anónimos

**Estado:** Documentada, resolución postergada a bloque futuro
**Fecha:** 2026-09-09
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, endpoints anónimos de identidad con respuesta uniforme.

---

## Contexto

Descubierta durante el smoke de cierre de Backend-Fix-2, ejercitando el runtime real contra Postgres.

El proyecto tiene dos endpoints anónimos hermanos que comparten el mismo diseño de seguridad: responden
**200 exista o no la cuenta**, para no convertirse en un oráculo de correos registrados. En ambos, la
respuesta es deliberadamente muda, así que **la bitácora es el único rastro del intento**.

| Endpoint | Acta que lo define | ¿Persiste la auditoría si la cuenta no existe? |
| --- | --- | --- |
| `POST /auth/verification-email/resend` | [A40](A40-verification-email-resend-endpoint.md) | **Sí** |
| `POST /auth/password-reset/request` | A8 (Bloque 5) | **No** |

Los dos enrolan su fila mediante `IAuditableCommand` y el `AuditingBehavior`, que llama a
`IAuditLogger.LogAsync` **antes** del handler y deja la escritura en el `ChangeTracker`. Quien la
confirma es el `SaveChangesAsync` del handler. De ahí sale la diferencia.

## Evidencia

Dos llamadas consecutivas a cada endpoint, una con un correo registrado y otra con uno inexistente, sobre
la base de desarrollo:

```
POST /auth/verification-email/resend  {"email":"paciente.demo@cauce.local"}   -> 200
POST /auth/verification-email/resend  {"email":"no.existe.jamas@cauce.local"} -> 200
   audit_logs: 2 filas verification_email_resend_request

POST /auth/password-reset/request     {"email":"no.existe.jamas@cauce.local"} -> 200
POST /auth/password-reset/request     {"email":"paciente.demo@cauce.local"}   -> 200
   audit_logs: 1 fila password_reset_request
```

## Causa

`ResendVerificationEmailCommandHandler` llama a `SaveChangesAsync` **incondicionalmente**, con el
comentario que lo justifica: «este handler confirma la transacción aunque no tenga cambios de negocio
propios que persistir».

`RequestPasswordResetCommandHandler` tiene un `return` temprano cuando la cuenta no existe, que sale
**antes** de su `SaveChangesAsync`. La fila enrolada se descarta al cerrar el scope.

**No es un defecto accidental.** El propio código lo declara:

> `// La auditoría (PasswordResetRequest / users, sin trigger) la enrola el AuditingBehavior antes`
> `// de este handler; solo se persiste cuando la cuenta existe y se llama a SaveChanges (acta A8).`

Es decir, hay dos decisiones documentadas que apuntan en sentidos opuestos: A40 resolvió auditar el
intento sobre un correo inexistente, A8 resolvió no hacerlo. **Lo que no está justificado es la
asimetría**, no cada decisión por separado.

## Lo que esta acta NO afirma

Para que un lector futuro no rederive una versión más fuerte de la que la evidencia sostiene:

- **No hay defecto en el reenvío de verificación.** Su auditoría persiste, incluida la del correo
  inexistente. Verificado en código y en runtime.
- **No existe `IAuditLogger.EnqueueAudit`.** El único método de la interfaz es `LogAsync`.
- **No está establecido que haya incumplimiento de la Ley N° 29733.** Ver abajo.

## La pregunta de compliance, planteada

Si un intento de restablecimiento sobre un correo **que no corresponde a ningún titular registrado**
constituye tratamiento de dato personal que deba quedar registrado, es una pregunta jurídica, no
técnica. Hay argumentos en los dos sentidos: la dirección de correo es dato personal aunque no haya
cuenta, pero también puede sostenerse que sin titular en el sistema no hay tratamiento que auditar.

Lo que sí es técnicamente indiscutible es el **valor forense**: sin esa fila no hay forma de detectar
una enumeración de correos contra el endpoint de reset, mientras que contra el de reenvío sí la hay.

Conviene resolverlo **antes de Deployment-1**, con criterio del área legal de la tesis, y unificar los
dos endpoints en el sentido que se decida.

## Nota sobre la cobertura de pruebas

Las pruebas unitarias de ambos handlers usan un doble de `IAuditLogger`, así que verifican que la
auditoría **se solicita**, no que **se persista**. La diferencia solo aparece contra una base real. Si
el bloque que resuelva esto agrega pruebas, deben ser de integración.

## Bloque de resolución

Por definir. Recomendado antes de Deployment-1 por la implicancia de trazabilidad.

## Referencias

- `src/Cauce.Application/Identity/UseCases/RequestPasswordReset/RequestPasswordResetCommandHandler.cs`
- `src/Cauce.Application/Identity/UseCases/ResendVerificationEmail/ResendVerificationEmailCommandHandler.cs`
- `src/Cauce.Application/Common/Behaviors/AuditingBehavior.cs`
- Acta [A40](A40-verification-email-resend-endpoint.md), que decidió auditar el intento uniforme.
- Acta A8 del Bloque 5, que fijó la auditoría en cuatro capas y la regla de no-duplicación.
