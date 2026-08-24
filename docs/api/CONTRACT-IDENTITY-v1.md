# Contrato de Identidad — Cauce API v1

**Versión:** 1.0 · **Fecha:** 13 de julio de 2026 · **Backend:** tag `v0.6.2-dev-seed`
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
| POST | `/api/v1/auth/login` | Anónimo | `auth-login` 10/min por IP | 200, 400, 401, 429, 500 |
| POST | `/api/v1/auth/logout` | Bearer JWT | Sin política | 204, 400, 401, 500 |
| POST | `/api/v1/auth/password-reset/request` | Anónimo | `auth-pwreset` 3/h por IP | 200, 400, 429, 500 |
| POST | `/api/v1/auth/password-reset/confirm` | Anónimo | `auth-pwreset` 3/h por IP | 200, 400, 429, 500 |
| GET | `/api/v1/patients/me/consent/pdf` | Bearer JWT · `Policy=Patient` | Sin política | 200 (`application/pdf`), 401, 403, 404, 500 |
| PUT | `/api/v1/users/me/fcm-token` | Bearer JWT (cualquier rol) | Sin política | 204, 400, 401, 403, 500 |

Evidencia de las políticas de rate limit: `src/Cauce.Api/Configuration/RateLimitingPolicies.cs:53-57`.
Evidencia de los códigos: atributos `[ProducesResponseType]` en `AuthController.cs:45-49, 86-89, 104-106, 122-124, 146-148`.

**Endpoints que NO existen** y que el móvil podría esperar:

| Endpoint esperado | Estado | Evidencia |
|---|---|---|
| `POST /auth/refresh` (renovación de token) | **NO EXISTE** | `AuthController.cs` no declara ninguna acción de refresh |
| `GET /me`, `/users/me`, `/auth/session` (identidad post-login) | **NO EXISTE** | `src/Cauce.Api/Controllers/UsersController.cs:37` solo declara `PUT me/fcm-token` |
| Texto y versión vigente del consentimiento (pre-aceptación) | **NO EXISTE** | `IConsentService` expone `GetCurrentText()` y `GetCurrentVersion()` pero ningún controller los publica |
| Reenvío del correo de verificación | **NO EXISTE** | Sin acción de reenvío en `src/Cauce.Api/Controllers/` |
| Canje de código de invitación después del registro | **NO EXISTE** | El código solo se acepta en `auth/register` (`AuthController.cs:70`) |
| Estado de bloqueo de cuenta y tiempo de espera | **NO EXISTE** | Ningún endpoint expone `locked_until` |

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

**Enlace del correo** (`src/Cauce.Infrastructure/Identity/ClientUrlProvider.cs:28-29`)

```
{AppBaseUrl}/auth/password-reset?token={plainToken}
```

En Development, `AppBaseUrl` vale `http://localhost:5074`
(`src/Cauce.Api/appsettings.Development.json:29`), que es la URL del **backend**. El backend no expone una
ruta `GET /auth/password-reset`, así que ese enlace hoy no resuelve a ninguna pantalla. Para el móvil,
`AppBaseUrl` tendría que apuntar a un deep link del esquema `cauce://`. **Este cambio de configuración no
está hecho.**

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
| 429 | `retryAfterSeconds` (int) y header `Retry-After` cuando el valor es mayor que 0 (`RateLimitingPolicies.cs:65-77`) |
| 409 `unconfirmed_allergens` | `detected` (bool) y `allergens` (array) (`ExceptionHandlingMiddleware.cs:92-93`) |

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
| **Casing de las claves de `errors`** | **PascalCase**, tal como los nombra FluentValidation (`Email`, `Password`, `ConsentTextHash`, `InvitationCode`, `FullName`, `ConsentDocumentVersion`) |

El casing de las claves merece atención. `Program.cs:131` configura
`PropertyNamingPolicy = JsonNamingPolicy.CamelCase`, pero **no** configura `DictionaryKeyPolicy`, y
`JsonSerializerDefaults.Web` tampoco lo hace. Las claves del diccionario `errors` **no se transforman a
camelCase**: llegan en PascalCase.

Ejemplo con datos ficticios:

```json
{
  "type": null,
  "title": "Error de validación",
  "status": 400,
  "detail": "Una o más reglas de validación no se cumplieron.",
  "errors": {
    "Email": ["'Email' no es una dirección de correo electrónico válida."],
    "Password": ["La contraseña debe contener al menos un dígito."]
  },
  "traceId": "00-3f1a9c2e7b4d5a6f8e0c1b2a3d4e5f60-1a2b3c4d5e6f7a8b-00",
  "errorCode": "validation_error"
}
```

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
| Rotación de refresh token | Realm Keycloak | `revokeRefreshToken: true` |
| **Renovación** | **NO EXISTE en el backend** | No hay endpoint de refresh. `KeycloakTokenClient` solo implementa `LoginAsync` (`grant_type=password`) y `LogoutAsync` (revocación) |
| Revocación | `POST /api/v1/auth/logout` | Revoca el refresh token en Keycloak |
| Uso | Todo endpoint protegido | Header `Authorization: Bearer {accessToken}` |

**Consecuencia operativa.** El backend no ofrece renovación. Con un access token de 15 minutos, el móvil
tiene dos caminos, y ninguno está implementado hoy:

1. Llamar directamente al token endpoint de Keycloak con `grant_type=refresh_token`. El backend no
   participa, así que la renovación no queda auditada.
2. Pedir al backend un endpoint de refresh nuevo. **No existe.**

Con `revokeRefreshToken: true`, cada renovación emite un refresh token nuevo e invalida el anterior. Quien
haga la rotación tiene que persistir el token nuevo. Hoy **ese manejo no está en el backend**: queda del lado
del cliente.

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

| Pieza | Estado | Evidencia |
|---|---|---|
| Configuración de fuerza bruta en Keycloak | **Activa** | `realm.json`: `bruteForceProtected: true`, `failureFactor: 5`, `waitIncrementSeconds: 60`, `maxFailureWaitSeconds: 900`, `permanentLockout: false` |
| `users.failed_login_attempts` | **Nadie lo incrementa** | `User.RegisterFailedLogin()` (`User.cs:218`) tiene 0 llamadores en `src/` |
| `users.locked_until` | **Nadie lo asigna** | `User.Lock()` (`User.cs:234`) solo se invoca desde `RegisterFailedLogin`, que nunca corre |
| `AccountLockedException` → 423 `account_locked` | **Código muerto** | Mapeada en `ExceptionHandlingMiddleware.cs:163-164`, con 0 sitios de `throw` |
| Endpoint que exponga el tiempo de espera | **NO EXISTE** | |

**Lo que ve el móvil hoy.** Keycloak bloquea la cuenta tras 5 fallos y responde 401 al token endpoint.
`KeycloakTokenClient.cs:61-65` colapsa 400 y 401 en `InvalidCredentialsException`, que sale como **401
`invalid_credentials`**, idéntico a una contraseña incorrecta. **El móvil no puede distinguir la cuenta
bloqueada ni mostrar el tiempo de espera que pide US05 CA02.**

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

### 8.5 Consecuencia para el móvil

El móvil tiene que enviar `consentDocumentVersion` y `consentTextHash`, pero **no tiene de dónde leer el
texto ni la versión**, porque no hay endpoint que los publique. Las opciones son:

| Opción | Riesgo |
|---|---|
| Replicar el texto en el cliente y hashearlo con SHA-256 UTF-8 hex minúscula | Cualquier diferencia de un byte (espacio, acento, salto de línea) produce un hash distinto y el registro falla siempre |
| Agregar al backend un endpoint que publique versión, texto y hash | **No existe.** Requiere decisión y cambio de backend |

Esta es una **brecha bloqueante para US01**. Se documenta, no se resuelve en este documento.

---

## 9. Rutas necesarias para US01, US05, US07 y US08

| US | Método | Ruta | Auth | Códigos |
|---|---|---|---|---|
| US01, US20 | POST | `/api/v1/auth/register` | Anónimo | 201, 400, 409, 429, 502, 500 |
| US01 CA04 | GET | `/api/v1/patients/me/consent/pdf` | `Policy=Patient` | 200, 401, 403, 404, 500 |
| US05 | POST | `/api/v1/auth/login` | Anónimo | 200, 400, 401, 429, 500 |
| US07 CA01 | POST | `/api/v1/auth/password-reset/request` | Anónimo | 200, 400, 429, 500 |
| US07 CA02 | POST | `/api/v1/auth/password-reset/confirm` | Anónimo | 200, 400, 429, 500 |
| US08 CA01 | POST | `/api/v1/auth/logout` | Bearer | 204, 400, 401, 500 |

**Rutas faltantes para cerrar estos US:** texto y versión del consentimiento (US01), estado de bloqueo con
tiempo de espera (US05 CA02), renovación de token (US08 CA02), reenvío de verificación de correo, y canje de
código de invitación después del registro (US20 CA02).

---

## 10. Historial

| Versión | Fecha | Cambios |
|---|---|---|
| 1.0 | 2026-07-13 | Versión inicial. Levantada del código en el tag `v0.6.2-dev-seed` para habilitar Mobile-1b. |
