# Contrato de Identidad — Cauce API v1

**Versión:** 1.1 · **Fecha:** 25 de agosto de 2026 · **Backend:** rama `feature/backend-fixes-pre-mobile-1b`
**Alcance:** endpoints de identidad que consume la app móvil Flutter (US01, US05, US07, US08, US20).

Fuente de verdad: el código de `src/Cauce.Api/Controllers/AuthController.cs` y los handlers de
`src/Cauce.Application/Identity/`. Cada afirmación de este documento lleva su evidencia en `archivo:línea`.

**Decisión de arquitectura vigente:** el móvil autentica contra `POST /api/v1/auth/login` del backend
(passthrough a Keycloak). No usa Authorization Code + PKCE directo contra Keycloak. Motivo: el
`AuditingMiddleware` detecta el login solo por la ruta `POST /auth/login`
(`src/Cauce.Api/Middleware/AuditingMiddleware.cs:46`), y `LoginCommandHandler` es el único que escribe
`users.last_login_at` (`src/Cauce.Application/Identity/UseCases/Login/LoginCommandHandler.cs:45`). Con PKCE
directo, `audit_logs` quedaría sin registro de accesos.

---

## 1. Tabla resumen

| Método | Ruta | Auth | Rate limit | Códigos declarados |
|---|---|---|---|---|
| POST | `/api/v1/auth/register` | Anónimo | `auth-register` 5/h por IP | 201, 400, 409, 429, 502, 500 |
| POST | `/api/v1/auth/login` | Anónimo | `auth-login` 10/min por IP | 200, 400, 401, **423**, 429, 500 |
| POST | `/api/v1/auth/refresh` **(nuevo, v1.1)** | Anónimo | `auth-refresh` 20/min por IP | 200, 400, 401, 429, 500 |
| POST | `/api/v1/auth/logout` | Bearer JWT | Sin política | 204, 400, 401, 500 |
| POST | `/api/v1/auth/password-reset/request` | Anónimo | `auth-pwreset` 3/h por IP | 200, 400, 429, 500 |
| POST | `/api/v1/auth/password-reset/confirm` | Anónimo | `auth-pwreset` 3/h por IP | 200, 400, 429, 500 |
| GET | `/api/v1/consent/current` **(nuevo, v1.1)** | Anónimo | `consent-current` 60/min por IP | 200, 429, 500 |
| GET | `/api/v1/patients/me/consent/pdf` | Bearer JWT · `Policy=Patient` | Sin política | 200 (`application/pdf`), 401, 403, 404, 500 |
| PUT | `/api/v1/users/me/fcm-token` | Bearer JWT (cualquier rol) | Sin política | 204, 400, 401, 403, 500 |

Evidencia de las políticas de rate limit: `src/Cauce.Api/Configuration/RateLimitingPolicies.cs`.
Evidencia de los códigos: atributos `[ProducesResponseType]` en `AuthController.cs`.

**Endpoints que siguen sin existir** y que el móvil podría esperar:

| Endpoint esperado | Estado | Evidencia |
|---|---|---|
| `GET /me`, `/users/me`, `/auth/session` (identidad post-login) | **NO EXISTE, pero ya no hace falta** | Desde v1.1 el login y la renovación devuelven el objeto `user` (§2.2 y §2.9) |
| Reenvío del correo de verificación | **NO EXISTE** | Sin acción de reenvío en `src/Cauce.Api/Controllers/` |
| Canje de código de invitación después del registro | **NO EXISTE** | El código solo se acepta en `auth/register` |

**Resueltos en v1.1:** `POST /auth/refresh`, `GET /consent/current`, y el estado de bloqueo con tiempo
de espera (423 `account_locked` con la extensión `lockedUntil`, §6).

---

## 2. Endpoints

### 2.1 `POST /api/v1/auth/register`

Registra un paciente. Devuelve el `userId` sin tokens. El paciente debe verificar su correo antes de poder
iniciar sesión.

| Campo | Valor |
|---|---|
| Auth | Anónimo (`[AllowAnonymous]`, `AuthController.cs:42`) |
| Rate limit | `auth-register`, 5 peticiones por hora por IP (`RateLimitingPolicies.cs:53`) |
| Header opcional | `Idempotency-Key` (UUID v4). Se valida el formato pero **no se usa para deduplicar** (`AuthController.cs:36, 55-61`) |

**Request** (`src/Cauce.Api/Contracts/Identity/RegisterPatientRequest.cs:2-7`)

| Campo | Tipo | Obligatorio | Validación (`RegisterPatientCommandValidator.cs`) |
|---|---|---|---|
| `email` | string | Sí | No vacío, máx 150, formato de correo |
| `fullName` | string | Sí | No vacío, longitud 2 a 150 |
| `password` | string | Sí | No vacío, mín 8, al menos una mayúscula, una minúscula y un dígito |
| `consentDocumentVersion` | string | Sí | No vacío |
| `consentTextHash` | string | Sí | Exactamente 64 caracteres, `^[0-9a-fA-F]{64}$` |
| `invitationCode` | string? | No | Si viene: longitud 8 a 20, `^[A-Z0-9]+$` |

La IP de origen no viaja en el body: el controller la toma de la conexión (`AuthController.cs:69`).

**Response 201** (`src/Cauce.Application/Identity/UseCases/RegisterPatient/RegisterPatientResult.cs:3-7`)

| Campo | Tipo | Notas |
|---|---|---|
| `userId` | Guid | Id local del usuario |
| `email` | string | |
| `status` | string (enum) | Siempre `"PendingActivation"` para un paciente nuevo (`src/Cauce.Domain/Identity/User.cs:124`) |
| `emailVerificationRequired` | bool | Siempre `true` (`RegisterPatientCommandHandler.cs:135`) |

**Errores**

| HTTP | `errorCode` | Cuándo |
|---|---|---|
| 400 | `validation_error` | Falla una regla de FluentValidation |
| 400 | `consent_text_mismatch` | La versión o el hash del consentimiento no coinciden con el documento vigente. Se evalúa **antes** que cualquier otra regla (`RegisterPatientCommandHandler.cs:60-63`) |
| 400 | `invalid_invitation_code` | El código no existe |
| 400 | `expired_invitation_code` | El código venció |
| 400 | `invitation_code_already_used` | El código ya se usó |
| 409 | `duplicate_email` | El correo ya está registrado |
| 429 | *(sin `errorCode`)* | Se superó el límite. Ver sección 3 |
| 502 | `keycloak_integration_error` | Falló el aprovisionamiento del usuario en Keycloak |
| 500 | `internal_server_error` | Error inesperado |

**Orden de validación en el handler** (importante para el móvil): consentimiento (línea 60), correo duplicado
(línea 65), código de invitación (línea 70). Un registro con hash de consentimiento incorrecto falla con
`consent_text_mismatch` aunque el correo también esté duplicado.

**Efecto en Keycloak:** el usuario se crea con `emailVerified: false` y la acción requerida `VERIFY_EMAIL`
(`src/Cauce.Infrastructure/Identity/KeycloakAdminClient.cs:74-76`). Ver sección 5.

---

### 2.2 `POST /api/v1/auth/login`

Passthrough a Keycloak con `grant_type=password` (Direct Access Grants).

| Campo | Valor |
|---|---|
| Auth | Anónimo (`[AllowAnonymous]`, `AuthController.cs:83`) |
| Rate limit | `auth-login`, 10 peticiones por minuto por IP (`RateLimitingPolicies.cs:54`) |

**Request** (`src/Cauce.Api/Contracts/Identity/LoginRequest.cs:2`)

| Campo | Tipo | Obligatorio | Validación (`LoginCommandValidator.cs:7-9`) |
|---|---|---|---|
| `email` | string | Sí | No vacío, formato de correo, máx 320 |
| `password` | string | Sí | No vacío, máx 200 |
| `clientId` | string | Sí | No vacío, máx 100. La app móvil envía `cauce-mobile` |

**Response 200** (`src/Cauce.Application/Identity/UseCases/Login/LoginCommand.cs:4-9`)

| Campo | Tipo | Notas |
|---|---|---|
| `accessToken` | string | JWT firmado por Keycloak |
| `refreshToken` | string | |
| `expiresIn` | int | Segundos. Valor del realm: 900 |
| `refreshExpiresIn` | int | Segundos |
| `tokenType` | string | `Bearer` |
| `user` | objeto | Identidad del usuario autenticado. Ver abajo |

**El response sí devuelve datos del usuario** desde el fix 5 (GAP 5). El objeto `user`
(`src/Cauce.Application/Identity/UseCases/Login/AuthenticatedUser.cs`) trae:

| Campo | Tipo | Notas |
|---|---|---|
| `userId` | Guid | Identificador local de la cuenta |
| `keycloakId` | string | Mismo valor que el claim `sub` del token |
| `email` | string | |
| `role` | string | `patient` o `nutritionist`. Coincide con `realm_access.roles` del JWT |
| `fullName` | string | |
| `emailVerified` | bool | |
| `isInActivePilot` | bool | **No viaja en ningún claim del token.** Este es el único punto del contrato donde el cliente puede conocerlo |

El móvil ya no necesita una segunda llamada ni decodificar el JWT para arrancar la sesión.

**Errores**

| HTTP | `errorCode` | Cuándo |
|---|---|---|
| 400 | `validation_error` | Falla una regla de FluentValidation |
| 401 | `invalid_credentials` | Keycloak respondió 400 o 401. Mensaje genérico, no revela si la cuenta existe (`KeycloakTokenClient.cs:61-65`) |
| 429 | *(sin `errorCode`)* | Se superó el límite |
| 500 | `user_local_missing` | Keycloak autenticó pero no existe la cuenta local. Inconsistencia de aprovisionamiento, no error del cliente; antes del fix 5 pasaba en silencio y se devolvían tokens de una identidad que el backend no conoce |
| 500 | `internal_server_error` | Keycloak respondió un status inesperado (`KeycloakTokenClient.cs:67-73`) |

**No se declara 423.** `AccountLockedException` existe y está mapeada a 423 `account_locked`
(`src/Cauce.Api/Middleware/ExceptionHandlingMiddleware.cs:163-164`), pero **ningún código la lanza**
(0 sitios de `throw` en `src/` y `tests/`). El bloqueo por fuerza bruta lo aplica Keycloak y llega al móvil
como 401 `invalid_credentials`. Ver sección 6.

**Llamada a Keycloak** (`src/Cauce.Infrastructure/Identity/KeycloakTokenClient.cs:45-52`)

| Parámetro del form | Valor |
|---|---|
| `grant_type` | `password` |
| `client_id` | El `clientId` que envió el cliente |
| `username` | El `email` |
| `password` | La contraseña |
| `scope` | `openid` |

**`client_secret`: no se envía.** El cliente `cauce-mobile` es público, así que no aplica.

**Efectos secundarios del login exitoso**

| Efecto | Evidencia |
|---|---|
| Escribe `users.last_login_at`, resetea `failed_login_attempts` a 0 y `locked_until` a null | `LoginCommandHandler.cs:45` → `User.cs:206-211` |
| Registra `LOGIN` en `audit_logs` | `AuditingMiddleware.cs:68-72, 108` |
| Un login fallido registra `FAILED_LOGIN` en `audit_logs` | `AuditingMiddleware.cs:60-66, 108` |

---

### 2.3 `POST /api/v1/auth/logout`

Revoca el refresh token en Keycloak.

| Campo | Valor |
|---|---|
| Auth | Bearer JWT requerido (`[Authorize]`, `AuthController.cs:102`) |
| Rate limit | Sin política |

**Request** (`src/Cauce.Api/Contracts/Identity/LogoutRequest.cs:2`)

| Campo | Tipo | Obligatorio | Validación (`LogoutCommandValidator.cs:7-8`) |
|---|---|---|---|
| `refreshToken` | string | Sí | No vacío |
| `clientId` | string | Sí | No vacío, máx 100 |

**Response 204.** Sin cuerpo.

**Errores**

| HTTP | `errorCode` | Cuándo |
|---|---|---|
| 400 | `validation_error` | Falla una regla de FluentValidation |
| 401 | *(sin `errorCode`)* | Falta el Bearer o el JWT es inválido o expiró |
| 500 | `internal_server_error` | Error inesperado |

Registra `LOGOUT` en `audit_logs` (`AuditingMiddleware.cs:73-76, 131`).

---

### 2.4 `POST /api/v1/auth/password-reset/request`

Solicita el restablecimiento. Responde 200 exista o no la cuenta, para no filtrar la existencia de correos.

| Campo | Valor |
|---|---|
| Auth | Anónimo (`AuthController.cs:119`) |
| Rate limit | `auth-pwreset`, 3 peticiones por hora por IP (`RateLimitingPolicies.cs:55`) |

**Request** (`RequestPasswordResetRequest.cs:2`)

| Campo | Tipo | Obligatorio | Validación (`RequestPasswordResetCommandValidator.cs:7-10`) |
|---|---|---|---|
| `email` | string | Sí | No vacío, máx 150, formato de correo |

**Response 200.** Sin cuerpo tipado.

**Errores:** 400 `validation_error` · 429 *(sin `errorCode`)* · 500 `internal_server_error`.

**Token de restablecimiento**

| Propiedad | Valor | Evidencia |
|---|---|---|
| Vigencia | 30 minutos | `src/Cauce.Domain/Identity/PasswordResetToken.cs:16` |
| Uso | Único | `PasswordResetToken.cs:106` (`!IsUsed && !IsExpired`) |
| Auditoría | `PasswordResetRequest` en `audit_logs` | `RequestPasswordResetCommand.cs:19, 25` (`IAuditableCommand`) |

**Enlace del correo** (`src/Cauce.Infrastructure/Identity/ClientUrlProvider.cs`) *(corregido en v1.1)*

El destino depende del cliente que originó la solicitud:

| `clientId` recibido | Enlace generado | Base configurable |
|---|---|---|
| `cauce-mobile` (o ausente) | `cauce://auth/password-reset?token={plainToken}` | `Email:MobileAppBaseUrl` |
| `cauce-web-portal` | `http://localhost:5173/auth/password-reset?token={plainToken}` | `Email:PortalAppBaseUrl` |

**Campo `clientId` del request:** opcional. Si se omite, el handler asume `cauce-mobile`. Se dejó
opcional para no romper a los clientes que ya consumían el endpoint ni el OpenAPI publicado. Un valor
desconocido devuelve **400 `validation_error`**.

Hasta v1.0 existía una sola base, `AppBaseUrl`, que en Development apuntaba a `http://localhost:5074`
—el propio backend, que no expone ninguna ruta fuera de `api/v{version}`—, así que el enlace del
correo devolvía 404 y **US07 no podía cerrarse end to end**.

---

### 2.5 `POST /api/v1/auth/password-reset/confirm`

Confirma el restablecimiento con el token recibido por correo.

| Campo | Valor |
|---|---|
| Auth | Anónimo (`AuthController.cs:143`) |
| Rate limit | `auth-pwreset`, 3 peticiones por hora por IP |

**Request** (`ConfirmPasswordResetRequest.cs:2`)

| Campo | Tipo | Obligatorio | Validación (`ConfirmPasswordResetCommandValidator.cs:7-14`) |
|---|---|---|---|
| `token` | string | Sí | No vacío |
| `newPassword` | string | Sí | No vacío, mín 8, al menos una mayúscula, una minúscula y un dígito |

**Response 200.** Sin cuerpo tipado.

**Errores**

| HTTP | `errorCode` | Cuándo |
|---|---|---|
| 400 | `validation_error` | Falla una regla de FluentValidation |
| 400 | `invalid_password_reset_token` | El token no existe |
| 400 | `expired_password_reset_token` | El token venció o ya se usó |
| 429 | *(sin `errorCode`)* | Se superó el límite |
| 500 | `internal_server_error` | Error inesperado |

El token viaja en claro y el backend lo compara por hash SHA-256 hex en minúscula
(`ConfirmPasswordResetCommandHandler.cs:75-79`). Auditado como `PasswordResetConfirm`
(`ConfirmPasswordResetCommand.cs:16, 22`).

---

### 2.6 `GET /api/v1/patients/me/consent/pdf`

Descarga el PDF del consentimiento aceptado (US01 CA04).

| Campo | Valor |
|---|---|
| Auth | Bearer JWT · `Policy=Patient` |
| Response 200 | `application/pdf` (binario) |
| Errores | 404 `consent_record_not_found` · 401 · 403 `forbidden` · 500 |

El PDF **se genera al vuelo** desde la fila persistida en `consent_records`, no desde una copia guardada
(`src/Cauce.Application/Identity/UseCases/GetMyConsentPdf/GetMyConsentPdfQueryHandler.cs:31-40`). El
contenido del PDF se arma con: nombre completo, correo, versión del documento, fecha de aceptación, hash del
texto e IP de origen. El nombre del archivo es `consentimiento-{documentVersion}.pdf`.

---

### 2.7 `PUT /api/v1/users/me/fcm-token`

| Campo | Valor |
|---|---|
| Auth | Bearer JWT (cualquier rol) |
| Request | `fcmToken` (string, nullable: enviar `null` desvincula el dispositivo) |
| Response | 204 |
| Errores | 400, 401, 403, 500 |

---

### 2.8 `GET /api/v1/consent/current` *(nuevo en v1.1)*

Publica el documento de consentimiento vigente. Es anónimo porque el paciente lo consulta **antes** de
registrarse, cuando todavía no tiene cuenta ni token.

| Campo | Valor |
|---|---|
| Auth | Anónimo (`[AllowAnonymous]`) |
| Rate limit | `consent-current`, 60 por minuto por IP |

**Response 200** (`src/Cauce.Application/Identity/UseCases/GetCurrentConsent/GetCurrentConsentQuery.cs`)

| Campo | Tipo | Notas |
|---|---|---|
| `version` | string | Versión vigente. Hoy `"1.0"` |
| `text` | string | Texto íntegro del documento |
| `hash` | string | SHA-256 del texto, hexadecimal minúscula, 64 caracteres |

**Por qué se publica el hash y no solo el texto.** El registro compara el hash recibido contra el del
documento vigente, y el cálculo **no aplica ninguna normalización**: ni `Trim()`, ni conversión de CRLF
a LF, ni normalización Unicode. El texto vigente mide 297 caracteres pero **301 bytes UTF-8** por las
tildes. Si el móvil replicara el texto y lo hasheara por su cuenta, cualquier diferencia de
codificación, BOM o salto de línea produciría un hash distinto y **todo intento de registro fallaría**
con 400 `consent_text_mismatch`, sin distinguir "versión desactualizada" de "hash incorrecto".

**Uso previsto:** llamar a este endpoint, y enviar en `POST /auth/register` exactamente los valores
`version` y `hash` recibidos. Recalcular el hash localmente es válido como verificación defensiva; el
invariante está cubierto por un test de integración.

---

### 2.9 `POST /api/v1/auth/refresh` *(nuevo en v1.1)*

Renueva la sesión a partir de un refresh token vigente.

| Campo | Valor |
|---|---|
| Auth | Anónimo (`[AllowAnonymous]`): la credencial es el propio refresh token |
| Rate limit | `auth-refresh`, 20 por minuto por IP |

**Request** (`src/Cauce.Api/Contracts/Identity/RefreshTokenRequest.cs`)

| Campo | Tipo | Obligatorio | Validación |
|---|---|---|---|
| `refreshToken` | string | Sí | No vacío |
| `clientId` | string | Sí | No vacío, máx 100, uno de `cauce-mobile` o `cauce-web-portal` |

**Response 200.** Idéntico al de `POST /auth/login`, incluido el objeto `user`.

**ROTACIÓN. El realm tiene `revokeRefreshToken: true` y `refreshTokenMaxReuse: 0`.** Cada renovación
emite un refresh token nuevo e invalida el anterior de inmediato. **El cliente debe persistir el token
que recibe**; reintentar con el anterior devuelve 401. Verificado end-to-end contra el stack real.

**Errores**

| HTTP | `errorCode` | Cuándo |
|---|---|---|
| 400 | `validation_error` | Falta un campo o el `clientId` no es conocido |
| 401 | `invalid_refresh_token` | El token expiró, fue revocado por un logout, o ya se consumió por una renovación previa |
| 429 | *(sin `errorCode`)* | Se superó el límite |
| 500 | `user_local_missing` | El token renovó pero no existe la cuenta local del sujeto |

**Auditoría.** Una renovación exitosa escribe `token_refresh` en `audit_logs` con el actor resuelto;
una fallida escribe `failed_token_refresh` **sin actor** (el token rechazado no permite identificar la
cuenta con garantías), conservando IP y momento. Lo escribe el handler, no el `AuditingMiddleware`.

---

## 3. Envelope de error (RFC 7807)

El middleware serializa un `ProblemDetails` con `Content-Type: application/problem+json`
(`ExceptionHandlingMiddleware.cs:25, 119-122`).

`Program.cs` **no** configura `ConfigureHttpJsonOptions`, así que `WriteAsJsonAsync` usa
`JsonSerializerDefaults.Web`: las **propiedades** salen en camelCase.

**Campos** (`ExceptionHandlingMiddleware.cs:290-301`)

| Campo | Tipo | Notas |
|---|---|---|
| `type` | string? | El middleware nunca lo asigna. Llega como `null`. No dependerse de él |
| `title` | string | Título legible, en español |
| `status` | int | Código HTTP |
| `detail` | string | Mensaje de la excepción |
| `instance` | string? | El middleware nunca lo asigna. Llega como `null` |
| `traceId` | string | Extensión. `Activity.Current?.Id` o `HttpContext.TraceIdentifier` |
| `errorCode` | string | Extensión. **Es el campo sobre el que el cliente debe hacer switch** |

Ejemplo con datos ficticios (correo ya registrado):

```json
{
  "type": null,
  "title": "Correo duplicado",
  "status": 409,
  "detail": "El correo electrónico ya está registrado.",
  "instance": null,
  "traceId": "00-3f1a9c2e7b4d5a6f8e0c1b2a3d4e5f60-1a2b3c4d5e6f7a8b-00",
  "errorCode": "duplicate_email"
}
```

**Extensiones adicionales por caso**

| Caso | Extensiones extra |
|---|---|
| 429 | `retryAfterSeconds` (int) y header `Retry-After` cuando el valor es mayor que 0 (`RateLimitingPolicies.cs`) |
| 409 `unconfirmed_allergens` | `detected` (bool) y `allergens` (array) (`ExceptionHandlingMiddleware.cs`) |
| **423 `account_locked`** *(v1.1)* | `lockedUntil` (fecha-hora ISO 8601 en UTC), el momento en que expira el bloqueo |

**El 429 no lleva `errorCode`.** El objeto que construye `RateLimitingPolicies.cs:71-77` solo trae `status`,
`title`, `detail` y `retryAfterSeconds`. El cliente debe detectar el 429 por el status HTTP, no por el
`errorCode`.

---

## 4. Errores de validación por campo (US01 CA02)

US01 CA02 exige "un mensaje de error específico por cada campo incorrecto". El backend produce **dos formas
distintas de 400**, y el móvil tiene que manejar las dos.

### 4.1 Validación de dominio (FluentValidation)

Ruta: `ValidationBehavior` lanza `ValidationException` (`src/Cauce.Application/Common/Behaviors/ValidationBehavior.cs:52`),
el middleware la convierte en `ValidationProblemDetails` (`ExceptionHandlingMiddleware.cs:271-288`).

| Característica | Valor |
|---|---|
| `errorCode` | `"validation_error"` |
| Campo con el detalle | `errors`, un diccionario `{ nombreDeCampo: [mensaje, ...] }` |
| **Casing de las claves de `errors`** | **camelCase** desde v1.1 (`email`, `password`, `consentTextHash`, `invitationCode`, `fullName`, `consentDocumentVersion`) |

Hasta v1.0 estas claves salían en **PascalCase**, inconsistentes con el resto del contrato. La causa:
`Program.cs` configura `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`, que aplica a **nombres de
propiedad** pero no a **claves de diccionario**, y `DictionaryKeyPolicy` no está configurado. Desde
v1.1 `BuildValidationProblem` normaliza la clave al agrupar los errores.

La conversión respeta rutas anidadas e indexadores: una regla sobre una colección produce
`items[0].quantity`, no `items[0].Quantity`.

Ejemplo con datos ficticios:

```json
{
  "type": null,
  "title": "Error de validación",
  "status": 400,
  "detail": "Una o más reglas de validación no se cumplieron.",
  "errors": {
    "email": ["'Email' no es una dirección de correo electrónico válida."],
    "password": ["La contraseña debe contener al menos un dígito."]
  },
  "traceId": "00-3f1a9c2e7b4d5a6f8e0c1b2a3d4e5f60-1a2b3c4d5e6f7a8b-00",
  "errorCode": "validation_error"
}
```

> Los **mensajes** siguen nombrando la propiedad en PascalCase (`'Email' no es...`) porque los genera
> FluentValidation. Solo cambió la clave del diccionario, que es lo que el cliente usa para asociar el
> error a su campo de formulario.

### 4.2 Validación de binding de `[ApiController]`

`BaseApiController` lleva `[ApiController]` (`src/Cauce.Api/Controllers/BaseApiController.cs:11`) y
`Program.cs` **no** define un `InvalidModelStateResponseFactory` propio. Un cuerpo JSON malformado, o un
campo obligatorio del record ausente, produce el `ValidationProblemDetails` automático de MVC:

| Característica | Valor |
|---|---|
| `errorCode` | **NO EXISTE** en esta respuesta |
| `errors` | Presente, con las claves que asigna el model binder |

**Regla para el móvil:** ante un 400, leer primero `errors` (siempre presente en ambos casos) y tratar
`errorCode` como opcional. No asumir que todo 400 trae `errorCode`.

---

## 5. Ciclo de vida del token

| Etapa | Dónde ocurre | Detalle |
|---|---|---|
| Obtención | `POST /api/v1/auth/login` | Devuelve `accessToken`, `refreshToken`, `expiresIn`, `refreshExpiresIn`, `tokenType` |
| Duración del access token | Realm Keycloak | 900 segundos (15 minutos). `accessTokenLifespan: 900` y `access.token.lifespan: "900"` del cliente `cauce-mobile` en `infrastructure/keycloak/import/realm.json` |
| Vida de la sesión del cliente móvil | Realm Keycloak | `client.session.max.lifespan: "2592000"` (30 días) |
| Rotación de refresh token | Realm Keycloak | `revokeRefreshToken: true`, `refreshTokenMaxReuse: 0` |
| **Renovación** *(v1.1)* | `POST /api/v1/auth/refresh` | Ver §2.9. Devuelve el mismo cuerpo que el login, con tokens nuevos |
| Revocación | `POST /api/v1/auth/logout` | Revoca el refresh token en Keycloak |
| Uso | Todo endpoint protegido | Header `Authorization: Bearer {accessToken}` |

### 5.1 El scope `offline_access` y por qué importa

El login del cliente `cauce-mobile` solicita **`scope=openid offline_access`**; cualquier otro cliente
recibe solo `openid` (`KeycloakTokenClient.ResolveScope`).

Sin `offline_access`, el refresh token queda atado a la sesión SSO del realm, cuyo
`ssoSessionIdleTimeout` es de **1800 segundos (30 minutos)**. Medido antes del fix, el login devolvía
`refreshExpiresIn: 1800`: la sesión moría por inactividad a la media hora, y **un endpoint de refresh
por sí solo no lo habría resuelto**. Con el scope pedido, el token pasa a regirse por
`offlineSessionIdleTimeout: 2592000` y el login devuelve `refreshExpiresIn: 2591999`, los 30 días que
prevé DEC-B3-01.

### 5.2 Obligación del cliente ante la rotación

Cada renovación invalida el refresh token presentado. El cliente **debe** sustituir el que tenga
guardado por el que recibe en la respuesta. Reintentar con el anterior devuelve 401
`invalid_refresh_token`, comportamiento verificado end-to-end contra el stack real.

**Validación del JWT en el backend** (`src/Cauce.Api/Program.cs:62-89`)

| Aspecto | Valor |
|---|---|
| Authority | `keycloakOptions.Authority` |
| Audiencia esperada | `keycloakOptions.Audience` (`cauce-backend`) |
| Claim de nombre | `preferred_username` |
| Claim de rol | `ClaimTypes.Role` |
| Mapeo de roles | `realm_access.roles` del JWT se copia a claims de rol de ASP.NET en `MapKeycloakRealmRoles` (`Program.cs:244-280`) |
| Políticas | `Patient` requiere el rol `patient`; `Nutritionist` requiere `nutritionist` (`Program.cs:87-89`) |
| Claim que identifica al usuario | `ClaimTypes.NameIdentifier`, con fallback a `sub` (`src/Cauce.Infrastructure/Identity/CurrentUserService.cs:18-19`). El valor es el `keycloak_id` |

---

## 6. Bloqueo de cuenta por intentos fallidos

**Funciona end to end desde v1.1.** La fuente de verdad del bloqueo es **Keycloak**; el backend solo
traduce su estado a un código que el cliente pueda interpretar.

| Pieza | Estado | Evidencia |
|---|---|---|
| Configuración de fuerza bruta en Keycloak | **Activa** | `realm.json`: `bruteForceProtected: true`, `failureFactor: 5`, `waitIncrementSeconds: 60`, `maxFailureWaitSeconds: 900`, `permanentLockout: false` |
| Detección del bloqueo | **Implementada** | `LoginCommandHandler` consulta `GET /admin/realms/{realm}/attack-detection/brute-force/users/{id}` cuando Keycloak rechaza las credenciales |
| `AccountLockedException` → 423 `account_locked` | **Vivo** | Se lanza desde `LoginCommandHandler`; el middleware añade la extensión `lockedUntil` |
| Tiempo de espera expuesto al cliente | **Sí** | Extensión `lockedUntil` (ISO 8601 UTC) en el cuerpo del 423 |
| `users.failed_login_attempts` / `users.locked_until` | **Código muerto documentado** | `User.RegisterFailedLogin()`, `User.IsLocked()` y `User.Unlock()` siguen sin llamadores en `src/`. Duplicar el contador crearía desincronía con el que Keycloak mantiene de verdad |

**Lo que ve el móvil.** Un 401 `invalid_credentials` significa contraseña incorrecta. Un **423
`account_locked`** significa cuenta bloqueada, y trae `lockedUntil` para mostrar la espera. Estando
bloqueada, **incluso la contraseña correcta devuelve 423**.

**Degradación deliberada.** Si la Admin API de Keycloak falla o no responde al consultar el estado, el
backend registra un warning y devuelve el **401 genérico**, no un 500. Un fallo de diagnóstico no debe
convertir un login rechazado en un error del servidor ni revelar al cliente que la consulta falló.

**Un correo no registrado nunca llega a consultar la Admin API:** responder distinto revelaría qué
cuentas existen.

> **Matiz observado en el stack real.** El bloqueo puede dispararse **antes** de los 5 intentos. El
> realm tiene `quickLoginCheckMilliSeconds: 1000` y `minimumQuickLoginWaitSeconds: 60`: dos fallos
> consecutivos en menos de un segundo activan la protección anti-ráfaga sin llegar al `failureFactor`.
> Ambos caminos marcan la cuenta como deshabilitada y producen el mismo 423, pero el cliente no debe
> asumir que hacen falta exactamente cinco intentos para ver un bloqueo.

---

## 7. Verificación de correo

| Pieza | Estado | Evidencia |
|---|---|---|
| El realm exige verificación | Sí | `realm.json`: `verifyEmail: true` |
| El usuario se crea sin verificar y con acción requerida | Sí | `KeycloakAdminClient.cs:74-76`: `emailVerified = !requireEmailVerification`, `requiredActions = ["VERIFY_EMAIL"]` |
| El registro pide verificación | Sí | `RegisterPatientCommandHandler.cs:75` pasa `requireEmailVerification: true` |
| Correo de verificación | Se dispara best-effort | `RegisterPatientCommandHandler.cs:131, 175-188`. Si falla, el registro igual queda válido |
| Endpoint de reenvío | **NO EXISTE** | |
| `users.email_verified` se actualiza al verificar | **NO** | `User.VerifyEmail()` solo lo invoca `DemoPatientSeeder.cs:94`. No hay listener de eventos de Keycloak |

**Comportamiento del login con el correo sin verificar.** Un usuario con la acción requerida `VERIFY_EMAIL`
pendiente no puede completar Direct Access Grants. `docs/dev/SEED-USERS.md:52-54` documenta el mismo
mecanismo para el nutricionista demo, que no puede autenticarse por Direct Grant mientras tenga pendiente
`UPDATE_PASSWORD`. Keycloak responde 400 o 401, y `KeycloakTokenClient.cs:61-65` lo colapsa a
**401 `invalid_credentials`**.

**El móvil no puede distinguir "correo sin verificar" de "contraseña incorrecta".** No hay test que cubra
este caso: los tests de integración usan `FakeKeycloakTokenClient`
(`tests/Cauce.Api.IntegrationTests/Identity/Support/FakeKeycloakTokenClient.cs`), y no existe ningún test de
`LoginCommandHandler`.

---

## 8. Consentimiento informado

Sección crítica. Si el hash no se calcula igual en el cliente y en el servidor, **el registro falla siempre**
con 400 `consent_text_mismatch`.

### 8.1 Origen del texto y la versión

| Dato | Key de configuración | Valor actual | Evidencia |
|---|---|---|---|
| Versión vigente | `Consent:CurrentVersion` | `"1.0"` | `src/Cauce.Api/appsettings.json:41` |
| Texto vigente | `Consent:Text` | Texto placeholder de una sola línea | `src/Cauce.Api/appsettings.json:42` |

No hay override de `Consent` en `appsettings.Development.json`. El texto actual es un placeholder: el
documento clínico definitivo lo provee el equipo del Complejo Hospitalario Guillermo Kaelín de la Fuente.

**No existe endpoint que publique el texto, la versión ni el hash.** La interfaz `IConsentService` declara
`GetCurrentVersion()`, `GetCurrentText()` y `GetCurrentTextHash()`
(`src/Cauce.Application/Common/Interfaces/Identity/IConsentService.cs:13-25`), pero ningún controller las
expone.

### 8.2 Cálculo del hash (código exacto)

`src/Cauce.Infrastructure/Identity/ConsentService.cs:47-51`

```csharp
private static string ComputeHash(string text)
{
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
    return Convert.ToHexString(bytes).ToLowerInvariant();
}
```

| Aspecto | Valor |
|---|---|
| Algoritmo | SHA-256 |
| Encoding de entrada | **UTF-8** |
| Normalización del texto | **Ninguna.** No hay `Trim()`, ni conversión de CRLF a LF, ni normalización Unicode. El texto se hashea byte a byte tal como sale de la configuración |
| Formato de salida | **Hexadecimal en minúscula**, 64 caracteres |
| Momento del cálculo | Una sola vez, en el constructor de `ConsentService` (`ConsentService.cs:28`) |

### 8.3 Verificación en el registro

`src/Cauce.Infrastructure/Identity/ConsentService.cs:41-45`

```csharp
public bool VerifyHash(string documentVersion, string clientProvidedHash)
{
    return string.Equals(documentVersion, _currentVersion, StringComparison.Ordinal)
        && string.Equals(clientProvidedHash, _currentTextHash, StringComparison.OrdinalIgnoreCase);
}
```

| Regla | Comportamiento |
|---|---|
| Versión | Comparación **Ordinal**, sensible a mayúsculas. `"1.0"` debe llegar exactamente así |
| Hash | Comparación **OrdinalIgnoreCase**. El cliente puede enviar el hex en mayúscula o minúscula |
| Resultado negativo | `RegisterPatientCommandHandler.cs:60-63` lanza `ConsentTextMismatchException` → **400 `consent_text_mismatch`** |

**El servidor recalcula el hash de su propio texto y lo compara.** No persiste lo que llega sin verificar. Si
la versión no coincide con la vigente, o el hash no coincide con el del texto vigente, el registro se rechaza
con el mismo `errorCode`: `consent_text_mismatch`. El contrato **no distingue** "versión desactualizada" de
"hash incorrecto".

### 8.4 Lo que se persiste

`ConsentRecord.Capture(...)` (`RegisterPatientCommandHandler.cs:86-93`) guarda: `userId`,
`documentVersion`, `consentTextHash`, `ipAddress`, `acceptedAt`. **No guarda el texto.**

### 8.5 Consecuencia para el móvil *(resuelta en v1.1)*

El móvil obtiene versión, texto y hash de **`GET /api/v1/consent/current`** (§2.8) y envía en el
registro exactamente los valores `version` y `hash` recibidos.

Hasta v1.0 esto era una **brecha bloqueante para US01**: el cliente tenía que enviar
`consentDocumentVersion` y `consentTextHash` sin ninguna fuente de donde leerlos, y la única
alternativa —replicar el texto y hashearlo— era frágil hasta el punto de romperse por un solo byte de
diferencia. El texto vigente mide 297 caracteres pero 301 bytes UTF-8.

Recalcular el hash en el cliente sigue siendo válido como verificación defensiva, pero ya no es la
única vía.

---

## 9. Rutas necesarias para US01, US05, US07 y US08

| US | Método | Ruta | Auth | Códigos |
|---|---|---|---|---|
| US01 | GET | `/api/v1/consent/current` | Anónimo | 200, 429, 500 |
| US01, US20 | POST | `/api/v1/auth/register` | Anónimo | 201, 400, 409, 429, 502, 500 |
| US01 CA04 | GET | `/api/v1/patients/me/consent/pdf` | `Policy=Patient` | 200, 401, 403, 404, 500 |
| US05 | POST | `/api/v1/auth/login` | Anónimo | 200, 400, 401, 423, 429, 500 |
| US07 CA01 | POST | `/api/v1/auth/password-reset/request` | Anónimo | 200, 400, 429, 500 |
| US07 CA02 | POST | `/api/v1/auth/password-reset/confirm` | Anónimo | 200, 400, 429, 500 |
| US08 CA01 | POST | `/api/v1/auth/logout` | Bearer | 204, 400, 401, 500 |
| US08 CA02 | POST | `/api/v1/auth/refresh` | Anónimo | 200, 400, 401, 429, 500 |

**Rutas que siguen faltando:** reenvío de verificación de correo, y canje de código de invitación
después del registro (US20 CA02). El texto del consentimiento (US01), el estado de bloqueo con tiempo
de espera (US05 CA02) y la renovación de token (US08 CA02) quedaron cubiertos en v1.1.

---

## 10. Historial

| Versión | Fecha | Cambios |
|---|---|---|
| 1.0 | 2026-07-13 | Versión inicial. Levantada del código en el tag `v0.6.2-dev-seed` para habilitar Mobile-1b. |
| 1.1 | 2026-08-25 | Cierre de los 8 gaps del `REPORTE-VERIFICACION-03`. Nuevos §2.8 `GET /consent/current` y §2.9 `POST /auth/refresh`. `POST /auth/login` incorpora el objeto `user` (§2.2) y declara 423 `account_locked` con la extensión `lockedUntil` (§6). El login del móvil pide `offline_access`, llevando `refreshExpiresIn` de 1800 a 2591999 (§5.1). Las claves de `errors` pasan a camelCase (§4.1). El enlace de recuperación se parametriza por cliente de origen. |
