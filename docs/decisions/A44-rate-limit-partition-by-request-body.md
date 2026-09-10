# Acta A44: Partición del rate limit por el correo del cuerpo de la petición

**Estado:** Aprobada, aplicada en Backend-Fix-2 Fase 2
**Fecha:** 2026-09-06
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Backend, pipeline HTTP y políticas de limitación de tasa. No cambia el contrato de API.

---

## Contexto

La decisión D3 del bloque Backend-Fix-2 fija que el endpoint `POST /api/v1/auth/verification-email/resend`
se limite a **3 peticiones por hora por correo normalizado**, no por IP. La elección es deliberada:

- Particionar por IP dejaría que un usuario legítimo detrás de una NAT compartida, como la red de un
  hospital, agotara el cupo de todos sus vecinos.
- Particionar por IP tampoco frenaría a quien hostiga un mismo buzón rotando direcciones de origen.

El obstáculo es técnico. Las siete políticas existentes derivan su clave de partición de datos que
`HttpContext` ya tiene resueltos de forma síncrona: `AddIpFixedWindow` lee
`Connection.RemoteIpAddress`, y `AddUserFixedWindow` lee el claim `sub`. El correo, en cambio, viaja en
el **cuerpo** de la petición, y ahí aparecen dos restricciones que se combinan mal:

1. Las fábricas de `RateLimitPartition` que acepta `RateLimiterOptions.AddPolicy` son **síncronas**. No
   hay sobrecarga asíncrona, ni en `IRateLimiterPolicy<TKey>`.
2. El cuerpo de una petición es un stream de **una sola lectura**. Leerlo desde la política exigiría
   I/O síncrona, que Kestrel rechaza por defecto (`AllowSynchronousIO = false`) y que además es un
   antipatrón conocido por el riesgo de agotar el thread pool.

## Decisión

Se agrega un middleware, `VerificationResendPartitionMiddleware`, registrado **antes** de
`app.UseRateLimiter()`. Solo actúa sobre `POST /auth/verification-email/resend`. En esa ruta:

1. Llama a `Request.EnableBuffering()`.
2. Lee el cuerpo de forma asíncrona y extrae la propiedad `email`.
3. La normaliza con `Trim()` y `ToLowerInvariant()`.
4. La deja en `HttpContext.Items` bajo la clave `cauce.verify-resend-email`.
5. Rebobina el stream para que el model binding del controlador lo consuma intacto.

La política `auth-verify-resend` lee esa entrada de `HttpContext.Items` de forma síncrona, que es todo
lo que su fábrica necesita.

**No es un patrón nuevo en este repo.** `AuditingMiddleware` ya bufferiza el cuerpo para leer el correo
del login (`AuditingMiddleware.cs:52` y `TryReadEmailAsync`, líneas 156-176). La única diferencia es la
posición en el pipeline: el de auditoría corre después de `UseAuthorization`, y este tiene que correr
antes del limitador.

## Fallback ante un cuerpo inutilizable

Si el cuerpo no es JSON válido, no es un objeto, o no trae `email` como cadena no vacía, la política
particiona por **la IP de origen**, con el prefijo `ip:` para que no colisione con un correo.

La alternativa evidente, una clave compartida tipo `"unknown"`, se descartó: crearía una cubeta única
que cualquiera podría agotar enviando tres cuerpos malformados, dejando sin servicio a todos los demás.
Caer a la IP acota el daño a quien lo provoca.

## Consecuencias

- Es la octava política de limitación de tasa del sistema, y la primera que particiona por contenido
  del cuerpo. Las otras siete no se tocan.
- El middleware bufferiza el cuerpo **solo** de esa ruta. Cualquier otra petición atraviesa el pipeline
  sin que se lea su cuerpo.
- La normalización se aplica en dos lugares que deben coincidir: el middleware, para la partición, y
  `AuthController.ResendVerificationEmail`, para el comando. Si divergen, una variación de mayúsculas
  eludiría el límite. Hay pruebas de integración que fijan ambas.
- Si en el futuro otro endpoint necesita particionar por cuerpo, conviene generalizar el middleware a
  una lista de rutas y nombres de propiedad, en vez de duplicarlo.

## Alternativas descartadas

| Alternativa | Motivo del descarte |
| --- | --- |
| Leer el cuerpo dentro de la política con I/O síncrona | Exige `AllowSynchronousIO = true` en Kestrel, con riesgo de agotar el thread pool |
| Particionar por IP y validar el límite por correo dentro del handler | El 429 dejaría de emitirlo el limitador, así que habría que replicar el `OnRejected` con su extensión `retryAfterSeconds` |
| Mover el correo a un header para que la política lo lea | Cambia el contrato del endpoint y expone en un header un dato que ya viaja en el cuerpo |

## Referencias

- `src/Cauce.Api/Middleware/VerificationResendPartitionMiddleware.cs`
- `src/Cauce.Api/Configuration/RateLimitingPolicies.cs` (`AuthVerifyResend`, `AddEmailFixedWindow`)
- `src/Cauce.Api/Program.cs` (orden del pipeline)
- `src/Cauce.Api/Middleware/AuditingMiddleware.cs` (patrón de bufferizado preexistente)
- Acta A40, endpoint de reenvío de verificación.
