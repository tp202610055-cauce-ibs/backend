# Reporte de Verificación 03 — Pre-Fix-Backend-Pre-Mobile-1b

**Ejecutado:** 20 de agosto de 2026
**Modo:** solo lectura. No se modificó ningún archivo de `src/`, `tests/`, `infrastructure/` ni `lib/`.
**Solicitado por:** Claude Kiwicha (prompt de verificación read-only, fechado 19 de agosto de 2026).
**Artefactos producidos:** este documento y `ESTADO-GAPS-BACKEND.md`. Nada más.

## Coordenadas de los repositorios

El proyecto es un **polyrepo** de cinco repositorios hermanos bajo `proyecto_final/`.

| Repo | Rama | SHA de HEAD | Último commit | Working tree |
|---|---|---|---|---|
| `backend` | `develop` | `b2dfd9c6ab5400d73c184a1ae3f64cc76017d2d9` | 2026-07-10 11:47 -0500 | 3 rutas sin trackear |
| `mobile` | `develop` | `f5220733b11baa01dc5849e65f48c11c1d363e82` | 2026-07-11 10:12 -0500 | limpio |
| `infrastructure` | `develop` | `988d5af` | — | `.env` sin trackear |
| `docs` | `develop` | `b866bfe` | — | 1 modificado + 11 sin trackear |
| `web-portal` | no inspeccionado (fuera del alcance del prompt) | | | |

> **Corrección a la precondición del prompt.** El prompt asume "~3.5 semanas sin actividad" desde el
> 14 de julio de 2026. El último commit real del backend es del **10 de julio** y el del mobile del
> **11 de julio**. A la fecha de ejecución han pasado **~5.8 semanas** sin commits en backend y
> **~5.7 semanas** en mobile. No hubo actividad del 14 de julio: los documentos fechados el 13 de julio
> (`CONTRACT-IDENTITY-v1.md`, `MATRIZ-IDENTIDAD.md`) existen en disco pero **nunca se commitearon**.

> **Corrección de nomenclatura.** El prompt usa `src/Cauce.API/` y `tests/Cauce.IntegrationTests/`.
> Los nombres reales son `src/Cauce.Api/` y `tests/Cauce.Api.IntegrationTests/`. Todas las rutas de
> evidencia de este reporte usan los nombres reales.

---

# FASE 0 · Estado del entorno

## 0.A Repo backend

**P0.1 ¿Cuál es la rama actual del backend?**
Respuesta: `develop`
Evidencia: `git branch --show-current`
Detalle: Existen además 8 ramas feature locales, todas con contraparte en `origin`.

**P0.2 ¿El working tree está limpio o hay cambios sin commitear?**
Respuesta: NO (hay cambios sin commitear)
Evidencia: `git status --short`
Detalle: Tres rutas **sin trackear**, ninguna modificación de archivos ya versionados:
`?? .claude/` · `?? CLAUDE.md` · `?? docs/api/CONTRACT-IDENTITY-v1.md`.
El `CLAUDE.md` del backend (v2.0.2, 2026-07-10) y el contrato de identidad **no están en git**.

**P0.3 ¿Existe el tag `v0.6.2-dev-seed`? ¿Es el más reciente?**
Respuesta: SÍ
Evidencia: `git tag --list`; `git rev-list -n 1 v0.6.2-dev-seed` → `b2dfd9c6ab5400d73c184a1ae3f64cc76017d2d9`
Detalle: Es el último de 10 tags y apunta exactamente a `HEAD` de `develop`.

**P0.4 ¿Hubo commits después de `v0.6.2-dev-seed`?**
Respuesta: NO
Evidencia: `git log --oneline v0.6.2-dev-seed..HEAD` devuelve vacío
Detalle: El tag y `HEAD` son el mismo commit. El backend no avanzó ni un commit desde el 10 de julio.

**P0.5 ¿Existe alguna rama tipo `feature/backend-fixes-*` o `feature/pre-mobile-1b-*`?**
Respuesta: NO
Evidencia: `git branch -a`
Detalle: Ramas existentes: `chore/dev-seed-patient`, `develop`, `main`, `feature/api-contract-hardening`,
`feature/auditing-notifications-workers`, `feature/clinical-registry`, `feature/patients-module`,
`feature/pilot-compliance`, `feature/recommendations-batch`, `feature/recommendations-module`.
Ninguna de fixes ni de pre-Mobile-1b, ni local ni remota.

## 0.B Repo mobile

**P0.6 ¿Cuál es la rama actual del mobile?**
Respuesta: `develop`
Evidencia: `git branch --show-current`

**P0.7 ¿Existe la rama `feature/mobile-1-identity`? ¿Local, remota, ambas?**
Respuesta: NO ENCONTRADO
Evidencia: `git branch -a`
Detalle: Ramas existentes: `archive/initial-prototype-idea`, `develop`, `main` (las tres con
contraparte remota). No existe ninguna rama de identidad.

**P0.8 ¿El working tree está limpio?**
Respuesta: SÍ
Evidencia: `git status --short` devuelve vacío

**P0.9 Últimos 10 commits del mobile**
Respuesta: SÍ (listados)
Evidencia: `git log --oneline -10`

| SHA | Fecha | Mensaje |
|---|---|---|
| `f522073` | 2026-07-11 10:12 | `ci(github-actions): add minimal analyze and test workflow` |
| `50db7e7` | 2026-07-11 10:12 | `docs(root): add CLAUDE.md and fix cross-reference paths` |
| `ef6aed2` | 2026-07-11 10:11 | `style(lints): enable strict analysis and formatting rules` |
| `01f2f77` | 2026-07-11 09:23 | `chore(env): add .env.example template` |
| `42a5d2e` | 2026-07-11 09:23 | `chore(openapi): import backend contract v1.0.0 snapshot` |
| `01aa0eb` | 2026-07-11 08:25 | `build(deps): declare dependency set for foundations` |
| `af8fb37` | 2026-07-10 23:46 | `chore(structure): add feature-first folder structure` |
| `24710fa` | 2026-07-10 23:40 | `chore(scaffold): reset develop to clean flutter create baseline` |
| `ef41fb5` | 2026-07-10 21:05 | `docs: add code conventions and decisions block for mobile` |
| `1487e4d` | 2026-07-10 16:50 | `feat: add widget tests` |

**P0.10 ¿Los 8 commits de Mobile-1a están en `develop`? Confirmar con SHAs.**
Respuesta: SÍ
Evidencia: `git log --oneline -12` sobre `develop`
Detalle: Los 8 commits de Mobile-1a son, del más antiguo al más reciente: `24710fa`, `af8fb37`,
`01aa0eb`, `42a5d2e`, `01f2f77`, `ef6aed2`, `50db7e7`, `f522073`. Están todos en `develop` y `f522073`
es `HEAD`. `ef41fb5` y anteriores pertenecen al prototipo previo al reset del scaffold.

## 0.C Herramientas

**P0.11 `flutter --version`**
Respuesta: SÍ
Evidencia: salida del comando

```
Flutter 3.44.2 • channel stable • https://github.com/flutter/flutter.git
Framework • revision c9a6c48423 (2 months ago) • 2026-06-10 15:52:41 -0700
Engine • hash 04efd7c093d4e9281d5526ebcad6ecc60ba8badf (revision 77e2e94772) (2 months ago) • 2026-06-10 19:59:06.000Z
Tools • Dart 3.12.2 • DevTools 2.57.0
```

Detalle: El CLI avisa que hay una versión más nueva disponible (`flutter upgrade`).

**P0.12 `dotnet --version`**
Respuesta: SÍ
Evidencia: salida del comando

```
9.0.203
Runtime Environment: Windows 10.0.26200, win-x64
Host Version: 9.0.4
MSBuild 17.13.20+a4ef1e90f
.NET workloads installed: (ninguno)
```

Detalle: Coincide exactamente con la versión fijada en `backend/CLAUDE.md` (9.0.203).

**P0.13 ¿Docker Desktop está corriendo? ¿Qué contenedores están arriba?**
Respuesta: PARCIAL
Evidencia: `tasklist` muestra `Docker Desktop.exe` (×3), `com.docker.backend.exe` (×2),
`com.docker.build.exe`; `docker ps` no responde (timeout de 45 s y de 90 s en dos intentos)
Detalle: **Los procesos de Docker Desktop existen pero el engine no responde al CLI.** Verificado por
sondeo directo de puertos, que no depende del CLI:

| Servicio | Puerto | Estado |
|---|---|---|
| Postgres | 5432 | CERRADO |
| Keycloak | 8081 | CERRADO |
| KeyDB | 6379 | CERRADO |
| MinIO API | 9000 | CERRADO |
| MinIO consola | 9001 | CERRADO |
| Mailpit UI | 8025 | CERRADO |
| Mailpit SMTP | 1025 | CERRADO |
| Adminer | 8080 | CERRADO |
| Cauce API | 5074 | CERRADO |
| **Ollama** | **11434** | **ABIERTO** |

**Ningún contenedor del stack Cauce está arriba.** El único puerto que responde es el 11434 (Ollama).
Consecuencia: las preguntas que exigen consultar PostgreSQL (P4.3 parcial, P14.1, P14.2, P14.3) no se
pueden responder contra datos reales, y los tests de integración se omiten (ver P13.1).

---

# FASE 1 · Los 8 gaps del backend

## GAP 1 · Endpoint `/auth/refresh` inexistente

**P1.1 ¿Existe endpoint `POST /api/v1/auth/refresh`?**
Respuesta: NO
Evidencia: `src/Cauce.Api/Controllers/AuthController.cs:19-169`
Detalle: `AuthController` declara exactamente cinco acciones: `register` (línea 43), `login` (84),
`logout` (103), `password-reset/request` (120) y `password-reset/confirm` (144). No hay ninguna acción
de refresh en ningún controller del proyecto.

**P1.2 ¿Existe algún método o handler que refresque tokens contra Keycloak?**
Respuesta: NO
Evidencia: `src/Cauce.Application/Common/Interfaces/Identity/IKeycloakTokenClient.cs:8-29` ·
`src/Cauce.Infrastructure/Identity/KeycloakTokenClient.cs:43-116`
Detalle: La interfaz declara solo `LoginAsync` y `LogoutAsync`. Un grep de `grant_type` en todo `src/`
devuelve tres coincidencias: `"password"` (`KeycloakTokenClient.cs:47`), `"client_credentials"`
(`KeycloakAdminClient.cs:268`) y una mención en un comentario. **`grant_type=refresh_token` no aparece
en ninguna parte del código.**

**P1.3 ¿El flujo de login guarda el refresh token de Keycloak en algún lado?**
Respuesta: PARCIAL
Evidencia: `src/Cauce.Application/Identity/UseCases/Login/LoginCommandHandler.cs:50-55`
Detalle: El refresh token **solo se devuelve al cliente** en el cuerpo de la respuesta. No se persiste
en base de datos ni en caché. El backend no lo retiene: el cliente es su único custodio.

**P1.4 ¿Existen DTOs tipo `RefreshTokenRequest` o `RefreshTokenResponse`?**
Respuesta: NO
Evidencia: `src/Cauce.Api/Contracts/Identity/` contiene 7 archivos: `ConfirmPasswordResetRequest.cs`,
`CreateNutritionistRequest.cs`, `LoginRequest.cs`, `LogoutRequest.cs`, `RegisterPatientRequest.cs`,
`RequestPasswordResetRequest.cs`, `UpdateFcmTokenRequest.cs`
Detalle: Ninguno relacionado con refresh, ni en `Contracts/` ni en `Application/`.

**P1.5 ¿El contrato OpenAPI en `mobile/openapi/openapi-v1.0.0.json` menciona `/auth/refresh`?**
Respuesta: NO
Evidencia: `grep -c "auth/refresh" mobile/openapi/openapi-v1.0.0.json` → `0`
Detalle: El snapshot del contrato importado al mobile (commit `42a5d2e`) no lo contiene, coherente con
que el endpoint no exista en el backend.

**P1.6 Shape exacto de `LoginResponse` actual**
Respuesta: SÍ
Evidencia: `src/Cauce.Application/Identity/UseCases/Login/LoginCommand.cs:23-28`

```csharp
public sealed record LoginResult(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    int RefreshExpiresIn,
    string TokenType);
```

En JSON (camelCase por `Program.cs:131`): `accessToken` (string), `refreshToken` (string),
`expiresIn` (int), `refreshExpiresIn` (int), `tokenType` (string).

## GAP 2 · Password reset link a URL inexistente

**P2.1 ¿Dónde vive el template del email de password reset?**
Respuesta: SÍ
Evidencia: `src/Cauce.Infrastructure/Email/EmailTemplates.cs:99-132`
Detalle: Dos métodos estáticos, `BuildPasswordResetText` (línea 99) y `BuildPasswordResetHtml`
(línea 122). Son strings interpolados de C#, no archivos de plantilla externos. Asunto en
`EmailTemplates.cs:17`. El envío lo hace `src/Cauce.Infrastructure/Email/SmtpEmailSender.cs`.

**P2.2 ¿A qué URL apunta el link del email actualmente?**
Respuesta: SÍ
Evidencia: `src/Cauce.Infrastructure/Identity/ClientUrlProvider.cs:26-30`

```csharp
public string BuildPasswordResetLink(string plainToken)
{
    var baseLink = $"{_appBaseUrl}/auth/password-reset";
    return QueryHelpers.AddQueryString(baseLink, "token", plainToken);
}
```

Template resuelto: `{Email:AppBaseUrl}/auth/password-reset?token={plainToken}`.
En Development, con `AppBaseUrl = "http://localhost:5074"`, el link concreto es
`http://localhost:5074/auth/password-reset?token=<uuid>`.

**Este link no resuelve.** `http://localhost:5074` es el **backend**, y el backend no expone ninguna
ruta `GET /auth/password-reset` (todas sus rutas cuelgan de `api/v{version}/...`, ver P9.1). El
usuario recibe un 404.

> Nota de discrepancia documental: `backend/CLAUDE.md` describe el link como
> `https://<frontend>/reset-password?token=<plaintext>`. El código real usa la ruta
> `/auth/password-reset`, no `/reset-password`. **El código gana.**

**P2.3 ¿Existe algún parámetro configurable de esa URL?**
Respuesta: SÍ
Evidencia: `src/Cauce.Api/appsettings.json` (sección `Email`, clave `AppBaseUrl`, valor `""`) ·
`src/Cauce.Api/appsettings.Development.json` (clave `AppBaseUrl`, valor `"http://localhost:5074"`) ·
`src/Cauce.Infrastructure/Email/EmailOptions.cs:52`
Detalle: `Email:AppBaseUrl` es la única palanca. No existe `appsettings.Production.json` ni
`appsettings.Testing.json` en el repo; solo `appsettings.json` y `appsettings.Development.json`.
**El fix mínimo es de configuración, no de código**, pero exige decidir el destino (deep link
`cauce://` para el móvil, o una página del portal web).

**P2.4 ¿Existe endpoint de confirmación que reciba token + nueva password?**
Respuesta: SÍ
Evidencia: `src/Cauce.Api/Controllers/AuthController.cs:144` (`[HttpPost("password-reset/confirm")]`)
Detalle: La ruta real es `POST /api/v1/auth/password-reset/confirm` (no
`/auth/password/reset/confirm` como supone el prompt).

Request — `src/Cauce.Api/Contracts/Identity/ConfirmPasswordResetRequest.cs`:
`{ "token": string, "newPassword": string }`

Response: **200 sin cuerpo tipado** (`AuthController.cs:146` declara
`[ProducesResponseType(StatusCodes.Status200OK)]` sin tipo). Errores declarados: 400, 429, 500.

**P2.5 ¿Existe algún endpoint web/HTML del backend que sirva el link al hacer click?**
Respuesta: NO
Evidencia: `src/Cauce.Api/Program.cs:228-229` solo llama a `MapControllers()` y
`MapHealthChecks("/api/v1/health/live")`
Detalle: No hay Razor Pages, ni MVC con vistas, ni `UseStaticFiles`, ni endpoint alguno fuera del
prefijo `api/v{version}`. Nada puede atender `GET /auth/password-reset`.

**P2.6 ¿Existe servicio de envío de emails abstracto?**
Respuesta: SÍ
Evidencia: `src/Cauce.Application/Common/Interfaces/Identity/IEmailSender.cs:6-80`
Detalle: Interfaz con cinco métodos. Implementación:
`src/Cauce.Infrastructure/Email/SmtpEmailSender.cs`. El método relevante:

```csharp
Task SendPasswordResetLinkAsync(
    string recipientEmail,
    string fullName,
    string resetLink,
    CancellationToken ct = default);
```

Los otros cuatro: `SendNutritionistTemporaryCredentialsAsync`, `SendReportReadyAsync`,
`SendReportPasswordAsync`, `SendAccountDeletionConfirmationAsync`.
Existe además `INotificationSender` (`Common/Interfaces/Notifications/INotificationSender.cs`) para el
canal push/email de notificaciones, que es otro subsistema.

## GAP 3 · Validation error keys en PascalCase, no camelCase

**P3.1 ¿Dónde está configurada la serialización JSON del backend?**
Respuesta: SÍ
Evidencia: `src/Cauce.Api/Program.cs:127-133`

```csharp
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
```

Detalle crítico: se usa `AddJsonOptions` (opciones **de MVC**). **`ConfigureHttpJsonOptions` NO se
llama en ninguna parte de `Program.cs`.** Esa distinción es la causa raíz del gap (ver P3.3).

**P3.2 ¿La política actual es CamelCase, PascalCase, default o SnakeCaseLower?**
Respuesta: PARCIAL — depende de qué se serializa
Evidencia: `Program.cs:131`
Detalle: Tres reglas simultáneas, y hay que distinguirlas:

| Qué se serializa | Política efectiva | Resultado |
|---|---|---|
| Propiedades de DTOs devueltos por controllers | `JsonNamingPolicy.CamelCase` (MVC) | camelCase ✔ |
| Propiedades de `ProblemDetails` del middleware | `JsonSerializerDefaults.Web` (default de `WriteAsJsonAsync`) | camelCase ✔ |
| **Claves del diccionario `errors`** | **ninguna** (`DictionaryKeyPolicy` sin configurar) | **PascalCase ✘** |

`JsonNamingPolicy.CamelCase` aplica a **nombres de propiedad**, no a **claves de diccionario**. Para
las claves haría falta `DictionaryKeyPolicy`, que no está configurado ni en MVC ni en
`JsonSerializerDefaults.Web`.

**P3.3 ¿Los ProblemDetails de RFC 7807 respetan esa política?**
Respuesta: PARCIAL
Evidencia: `src/Cauce.Api/Middleware/ExceptionHandlingMiddleware.cs:119-122`

```csharp
context.Response.Clear();
context.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
context.Response.ContentType = ProblemJsonContentType;
await context.Response.WriteAsJsonAsync(problemDetails, problemDetails.GetType()).ConfigureAwait(false);
```

Detalle: El middleware es custom (`ExceptionHandlingMiddleware`, registrado en `Program.cs:193`), no
usa `IExceptionHandler` ni un `ProblemDetailsFactory` custom. **`HttpResponse.WriteAsJsonAsync` no usa
las opciones de MVC**: usa `Microsoft.AspNetCore.Http.Json.JsonOptions`, que por defecto es
`JsonSerializerDefaults.Web`. Por eso `status`, `title`, `detail`, `traceId` y `errorCode` sí salen en
camelCase, pero las claves de `errors` no se transforman.

**P3.4 Test de integración que valide el formato de un ProblemDetails de validación**
Respuesta: NO ENCONTRADO
Evidencia: `grep -rn "ProblemDetails\|\"errors\"\|errorCode\|ValidationProblem" tests/ --include=*.cs`
devuelve **cero coincidencias**
Detalle: **No existe ningún test, en ninguno de los cuatro proyectos de test, que asserte sobre el
envelope de error.** Ni sobre `errorCode`, ni sobre `errors`, ni sobre el casing. Es un hallazgo
relevante para la Fase 4: arreglar el GAP 3 no puede romper ningún test porque no hay ninguno que lo
cubra.

**P3.5 ¿Cómo genera el handler de FluentValidation las claves del diccionario `errors`?**
Respuesta: SÍ
Evidencia: `src/Cauce.Api/Middleware/ExceptionHandlingMiddleware.cs:271-288`

```csharp
private static ProblemDetails BuildValidationProblem(ValidationException exception, string traceId)
{
    var errors = exception.Errors
        .GroupBy(failure => failure.PropertyName)
        .ToDictionary(
            group => group.Key,
            group => group.Select(failure => failure.ErrorMessage).ToArray());

    var problem = new ValidationProblemDetails(errors)
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "Error de validación",
        Detail = "Una o más reglas de validación no se cumplieron."
    };
    problem.Extensions["traceId"] = traceId;
    problem.Extensions["errorCode"] = "validation_error";
    return problem;
}
```

Detalle: La clave es `ValidationFailure.PropertyName`, que FluentValidation deriva de la expresión
`RuleFor(x => x.Email)` → literal `"Email"`, en PascalCase. El `ValidationBehavior`
(`src/Cauce.Application/Common/Behaviors/ValidationBehavior.cs:52`) solo lanza
`new ValidationException(failures)` sin tocar nombres. **El punto exacto de fix son estas 5 líneas** (o,
alternativamente, un `ConfigureHttpJsonOptions` con `DictionaryKeyPolicy = JsonNamingPolicy.CamelCase`).

## GAP 4 · Endpoint consent text/hash inexistente

**P4.1 ¿Existe endpoint `GET /api/v1/consent/current` o `/consents/latest`?**
Respuesta: NO
Evidencia: `grep -rn "^\[Route" src/Cauce.Api/Controllers/` — no existe ningún `ConsentController`;
las únicas rutas con "consent" son `PatientsController.cs:169` (`[HttpGet("me/consent/pdf")]`)
Detalle: El único endpoint de consentimiento es `GET /api/v1/patients/me/consent/pdf`, que exige
`Policy = Patient` y devuelve el PDF del consentimiento **ya aceptado**. **No sirve para el registro**:
el paciente aún no tiene cuenta ni token cuando necesita el texto.

**P4.2 ¿Dónde vive el texto actual del consentimiento?**
Respuesta: SÍ
Evidencia: `src/Cauce.Api/appsettings.json`, sección `Consent`

```json
"Consent": {
  "CurrentVersion": "1.0",
  "Text": "Yo acepto participar voluntariamente en el piloto clínico del sistema Cauce. [El texto definitivo será provisto por el equipo clínico del Complejo Hospitalario Guillermo Kaelín de la Fuente antes del inicio del piloto. Este placeholder satisface el cumplimiento estructural durante el desarrollo.]"
}
```

Detalle: Vive **en configuración**, no en base de datos ni en un archivo de recursos. No hay override
de `Consent` en `appsettings.Development.json`. Se enlaza a
`src/Cauce.Infrastructure/Identity/ConsentDocumentOptions.cs`.

**P4.3 ¿Existe tabla `consents`, `consent_templates` o similar? ¿Cuántas filas tiene?**
Respuesta: NO (para tabla de plantillas) / NO VERIFICABLE (para conteo de filas)
Evidencia: `src/Cauce.Infrastructure/Persistence/Migrations/20260624212001_AddIdentityTables.cs` ·
`src/Cauce.Infrastructure/Persistence/Configurations/ConsentRecordConfiguration.cs`
Detalle: La única tabla de consentimiento es **`consent_records`**, que guarda **aceptaciones de
pacientes** (`userId`, `documentVersion`, `consentTextHash`, `ipAddress`, `acceptedAt`), **no el texto
del documento**. No existe tabla de plantillas. El conteo de filas no se puede obtener: Postgres está
caído (P0.13).

**P4.4 ¿La entidad `Patient`, `User` o `PatientProfile` almacena `consent_hash` y `consent_version`?**
Respuesta: PARCIAL
Evidencia: `src/Cauce.Domain/Identity/ConsentRecord.cs`
Detalle: Los almacena una entidad **dedicada**, `ConsentRecord` (tabla `consent_records`), no `User`
ni `PatientProfile`. Nota: la carpeta que el prompt supone, `src/Cauce.Domain/Aggregates/`, **no
existe**; el dominio se organiza por módulo (`Identity/`, `Patients/`, etc.).

**P4.5 ¿Cómo se computa hoy el hash del consentimiento?**
Respuesta: SÍ
Evidencia: `src/Cauce.Infrastructure/Identity/ConsentService.cs:47-51`

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
| Encoding de entrada | UTF-8 |
| Normalización | **Ninguna.** Sin `Trim()`, sin CRLF→LF, sin normalización Unicode |
| Salida | Hexadecimal minúscula, 64 caracteres |
| Momento | Una sola vez, en el constructor (`ConsentService.cs:28`) |

La verificación (`ConsentService.cs:41-45`) compara la versión con `StringComparison.Ordinal`
(sensible a mayúsculas) y el hash con `OrdinalIgnoreCase`.

**P4.6 ¿En qué endpoint se envía el `consent_hash` cuando el paciente se registra?**
Respuesta: SÍ
Evidencia: `src/Cauce.Api/Controllers/AuthController.cs:43` ·
`src/Cauce.Api/Contracts/Identity/RegisterPatientRequest.cs:40-46`
Detalle: `POST /api/v1/auth/register`, campos `consentDocumentVersion` y `consentTextHash` del cuerpo.
El validator exige exactamente 64 caracteres hex. Si no coincide, el handler lanza
`ConsentTextMismatchException` → **400 `consent_text_mismatch`**, y lo evalúa **antes** que el correo
duplicado y que el código de invitación.

## GAP 5 · Login no retorna user data

**P5.1 Shape exacto de la respuesta actual de `POST /api/v1/auth/login`**
Respuesta: SÍ
Evidencia: `src/Cauce.Application/Identity/UseCases/Login/LoginCommand.cs:23-28` (tipo) ·
`src/Cauce.Api/Controllers/AuthController.cs:86` (`[ProducesResponseType(typeof(LoginResult), 200)]`)

```json
{
  "accessToken": "string",
  "refreshToken": "string",
  "expiresIn": 900,
  "refreshExpiresIn": 2592000,
  "tokenType": "Bearer"
}
```

**P5.2 ¿Retorna solo tokens, tokens + user data, o solo un token bag?**
Respuesta: **Solo un token bag**
Evidencia: `LoginCommand.cs:23-28`
Detalle: Cero campos de identidad. No trae `userId`, `keycloakId`, `email`, `role`, `fullName`,
`emailVerified` ni `isInActivePilot`.

**Dato relevante para el fix:** el handler **ya carga la entidad `User`** para actualizar
`last_login_at` (`LoginCommandHandler.cs:42-48`). Los datos están en memoria y se descartan. Enriquecer
la respuesta no cuesta una query extra. Ojo con el borde: `user` puede ser `null` (línea 43) si Keycloak
autentica pero no hay fila local — ese caso hoy pasa silenciosamente.

**P5.3 ¿Existe un DTO tipo `AuthenticatedUserDto` o `UserProfileDto` reutilizable?**
Respuesta: NO
Evidencia: `src/Cauce.Api/Contracts/Identity/` (7 archivos, ninguno de perfil de usuario)
Detalle: No hay DTO de identidad de usuario reutilizable. Lo más cercano es
`RegisterPatientResult(Guid UserId, string Email, UserStatus Status, bool EmailVerificationRequired)`,
que es específico del registro, y `MyProfileSummaryResult`, que es del módulo Patients y trae datos
clínicos (no sirve como payload de login).

**P5.4 ¿Existe endpoint `GET /api/v1/users/me` o `GET /api/v1/patients/me`?**
Respuesta: PARCIAL
Evidencia: `src/Cauce.Api/Controllers/UsersController.cs:37` ·
`src/Cauce.Api/Controllers/PatientsController.cs:94, 208`
Detalle:

| Ruta | Existe | Notas |
|---|---|---|
| `GET /api/v1/users/me` | **NO** | `UsersController` solo declara `PUT me/fcm-token` (línea 37) |
| `GET /api/v1/patients/me` | **NO** | `PatientsController` declara `DELETE me` (190), no `GET me` |
| `GET /api/v1/patients/profile` | SÍ | `Policy = Patient`. Devuelve `GetPatientProfileResult`: perfil clínico completo (IMC, subtipo SII, alergias, nutricionista). Falla con 404 `patient_profile_not_found` si el onboarding no terminó |
| `GET /api/v1/patients/me/summary` | SÍ | `Policy = Patient`. `MyProfileSummaryResult`: nombre, correo enmascarado, datos clínicos, evolución IBS-SSS |

**Ninguno sirve como "quién soy" post-login genérico:** los dos que existen exigen `Policy = Patient`
(un nutricionista no los puede llamar) y devuelven datos clínicos, no identidad. Y ninguno expone
`isInActivePilot`.

**P5.5 Según `CONTRACT-IDENTITY-v1.md`, ¿qué campos mínimos necesita el mobile?**
Respuesta: PARCIAL
Evidencia: `backend/docs/api/CONTRACT-IDENTITY-v1.md:131-133`
Detalle: El documento **no publica una lista de campos mínimos requeridos**. Lo que declara es la
ausencia y la mitigación actual:

> "**El response NO devuelve datos del usuario.** No trae `userId`, `role`, `fullName` ni
> `emailVerified`. El móvil debe leer esos datos de los claims del `accessToken` (`sub`,
> `realm_access.roles`, `email`, `preferred_username`)."

Es decir, el contrato hoy resuelve el gap **empujando el trabajo al cliente** (decodificar el JWT), no
pidiendo campos nuevos. Los cuatro campos que nombra —`userId`, `role`, `fullName`, `emailVerified`—
más `isInActivePilot` (que el prompt menciona y que **no está en ningún claim** ni en ningún endpoint)
son el conjunto candidato. **`isInActivePilot` es el único que el móvil no puede obtener por ningún
medio hoy.**

## GAP 6 · Login flow con 0 cobertura de tests

**P6.1 ¿Existe `AuthControllerTests.cs` o `LoginCommandHandlerTests.cs`?**
Respuesta: NO
Evidencia: `tests/Cauce.Application.Tests/Identity/` contiene 6 archivos:
`ConfirmPasswordResetCommandHandlerTests.cs`, `CreateNutritionistCommandHandlerTests.cs`,
`GenerateInvitationCodeCommandHandlerTests.cs`, `RegisterPatientCommandHandlerTests.cs`,
`RequestPasswordResetCommandHandlerTests.cs`, `UpdateFcmTokenCommandHandlerTests.cs`
Detalle: **No existe `LoginCommandHandlerTests.cs` ni `LogoutCommandHandlerTests.cs` ni
`AuthControllerTests.cs`.** De los cinco endpoints de `AuthController`, tres tienen test de handler
(register, password-reset request, password-reset confirm) y **dos no tienen ninguno: login y logout**.

**P6.2 ¿Cuántos test methods hay para el flujo de login? Listar nombres.**
Respuesta: PARCIAL — 3 tests lo ejercitan, ninguno lo tiene como sujeto
Evidencia: `tests/Cauce.Api.IntegrationTests/Auditing/AuditMiddlewareTests.cs:25-79`
Detalle: Los únicos tests que tocan `/api/v1/auth/login` o `/auth/logout` son de auditoría:

| Test | Línea | Qué asserta |
|---|---|---|
| `Login_ValidCredentials_WritesLoginAuditWithActor` | 26 | 200 OK + una fila `Login` en `audit_logs` con el actor correcto |
| `Login_InvalidCredentials_Returns401AndWritesFailedLoginAudit` | 45 | 401 + una fila `FailedLogin` con el actor |
| `Logout_Authenticated_WritesLogoutAuditWithActor` | 64 | 204 + una fila `Logout` con el actor |

**Ninguno assertea el cuerpo de la respuesta.** Verifican status code y filas de `audit_logs`. La
matriz de cobertura declaraba CAs verdes que se apoyan en estos tres tests colaterales.

**P6.3 ¿Hay tests que cubran `AccountLockedException`, `InvalidCredentialsException`,
`InactiveUserException`, `EmailNotVerifiedException`?**
Respuesta: PARCIAL
Evidencia: `AuditMiddlewareTests.cs:45-61` · grep de `AccountLockedException` en `tests/` → 0 resultados

| Excepción | Cubierta | Notas |
|---|---|---|
| `InvalidCredentialsException` | **PARCIAL** | Se ejercita indirectamente en `AuditMiddlewareTests.cs:45` vía `FakeKeycloakTokenClient`, que la lanza si la contraseña no coincide. No hay test unitario del handler |
| `AccountLockedException` | **NO** | 0 referencias en `tests/`. Nunca se lanza (ver GAP 8) |
| `InactiveUserException` | **NO ENCONTRADO** | La excepción **no existe** en el código |
| `EmailNotVerifiedException` | **NO ENCONTRADO** | La excepción **no existe** en el código |

Las dos últimas no tienen test porque no existe la excepción. Un correo sin verificar hoy sale como
401 `invalid_credentials`, indistinguible de contraseña incorrecta.

**P6.4 ¿Los tests de integración de auth usan Testcontainers reales o mockean?**
Respuesta: PARCIAL — **Postgres real, Keycloak mockeado**
Evidencia: `tests/Cauce.Api.IntegrationTests/Identity/Support/PostgresFixture.cs:31-36` ·
`tests/Cauce.Api.IntegrationTests/Identity/Support/FakeKeycloakTokenClient.cs:11-45`
Detalle:

- **Postgres: real**, vía Testcontainers, imagen `postgres:16-alpine`. Si Docker no está, el fixture
  marca `IsAvailable = false` y los tests se omiten en vez de fallar (`PostgresFixture.cs:38-41`).
- **Keycloak: doble en memoria.** `FakeKeycloakTokenClient` devuelve `"fake-access-token"` si la
  contraseña es `"Correct123!"` y lanza `InvalidCredentialsException` si no. **No existe ningún
  contenedor de Keycloak en los tests.**
- Los JWT los firma localmente `TestJwtBuilder`, ya con `aud` y `sub` correctos.

**Consecuencia de fondo:** ninguna de las condiciones reales de Keycloak (lockout por fuerza bruta,
`VERIFY_EMAIL` pendiente, audience mapper, scope `basic`) se ejercita jamás en la suite. Es
exactamente el mecanismo que dejó ocultos los defectos de las actas A35 y A36.

## GAP 7 · Keycloak realm.json desactualizado

**P7.1 ¿Dónde vive el realm.json en el repo?**
Respuesta: SÍ
Evidencia: `infrastructure/keycloak/import/realm.json` (157 líneas)
Detalle: Un único archivo, en el repo `infrastructure`. No hay `realm-export.json` ni
`cauce-realm.json` en ningún otro lado.

**P7.2 ¿El JSON contiene un `protocolMapper` de tipo `oidc-audience-mapper` para `cauce-mobile`?**
Respuesta: **NO**
Evidencia: `grep -n "oidc-audience-mapper\|included.client.audience\|cauce-backend-audience"
infrastructure/keycloak/import/realm.json` → **exit 1, cero coincidencias**
Detalle: El archivo **no tiene sección `clientScopes`, ni ningún array `protocolMappers` en ningún
cliente**. El client scope `cauce-backend-audience` del acta A35 no está. Un realm importado desde este
archivo emite tokens con `aud: "account"`, que el backend rechaza con **401** por mismatch de audiencia
(`Program.cs:74`, `ValidAudience = "cauce-backend"`).

**P7.3 ¿`cauce-mobile` tiene los scopes `openid`, `profile`, `email` como `defaultClientScopes`?**
Respuesta: PARCIAL
Evidencia: `infrastructure/keycloak/import/realm.json:101`

```json
"defaultClientScopes": ["web-origins", "profile", "roles", "email"],
"optionalClientScopes": ["address", "phone", "offline_access", "microprofile-jwt"]
```

Detalle:
- `profile` ✔ y `email` ✔ están presentes.
- `openid` **no aplica como client scope**: en Keycloak es un scope de *request* (el backend ya lo
  envía en `KeycloakTokenClient.cs:51`), no un default client scope. La pregunta parte de una premisa
  equivocada, pero el efecto práctico es correcto.
- **Falta `basic`** (acta A36). En Keycloak 25 el mapper del claim `sub` vive dentro del scope `basic`.
  Sin él, el token no trae `sub`, y todo endpoint que resuelve el usuario local vía
  `CurrentUserService` lanza `UnauthorizedAccessException` → **403**.
- **Falta `cauce-backend-audience`** (acta A35).

**Los dos fixes de la sesión del 8-10 de julio son exactamente los dos que faltan.**

**P7.4 ¿`cauce-mobile` está configurado con `"directAccessGrantsEnabled": true`?**
Respuesta: SÍ
Evidencia: `infrastructure/keycloak/import/realm.json:84`
Detalle: `"directAccessGrantsEnabled": true` y `"publicClient": true` (línea 80). Coherente con el
passthrough `grant_type=password` sin `client_secret` que hace `KeycloakTokenClient.LoginAsync`.

**P7.5 ¿Existe cliente `cauce-web-portal`? ¿Con qué configuración?**
Respuesta: SÍ
Evidencia: `infrastructure/keycloak/import/realm.json:125-155`

| Atributo | Valor | Consecuencia |
|---|---|---|
| `publicClient` | `false` | **Confidencial** — exige `client_secret` |
| `secret` | `"REGENERAR_DESPUES_DEL_IMPORT"` | Placeholder, hay que regenerarlo en consola |
| `standardFlowEnabled` | `true` | Authorization Code habilitado |
| `directAccessGrantsEnabled` | **`false`** | **`POST /auth/login` no funciona para el portal** |
| `redirectUris` | `http://localhost:3000/*`, `http://localhost:5173/*` | |
| `defaultClientScopes` | `["web-origins","profile","roles","email"]` | Sin `basic` ni audience mapper |
| `client.session.max.lifespan` | `28800` (8 h) | Coherente con DEC-B3-01 |

Detalle: Confirma la deuda técnica ya conocida ("Secret de cliente confidencial en login" en
`CLAUDE.md`). `KeycloakTokenClient.LoginAsync` no envía `client_secret` y el cliente tiene Direct
Access Grants apagado: **el passthrough de login es imposible para el portal web tal como está**. No
bloquea Mobile-1b, pero sí el portal.

**P7.6 Fecha de última modificación del realm.json. ¿Anterior o posterior al 14 julio 2026?**
Respuesta: **ANTERIOR**
Evidencia: `git log -1 --format=%ai -- keycloak/import/realm.json` →
`b5079ee | 2026-06-23 00:11:11 -0500 | feat: add realm.json`
Detalle: **El archivo tiene un solo commit, del 23 de junio de 2026.** Nunca se tocó desde entonces:
ni el 8 de julio (sesión de los fixes de consola), ni el 10 de julio (fecha de las actas A35 y A36),
ni el 14 de julio. Las actas lo dicen explícitamente:

> A35: "El cambio es configuración manual de la consola de Keycloak, **no se reejecuta desde
> `realm.json`**. Toda futura importación de `realm.json` debe verificar la presencia de este scope."

> A36: "Reasignar el scope `basic` como default (…) **desde la consola de administración**."

Un `docker compose down -v` seguido de re-import **pierde ambos fixes en silencio** y deja la API
devolviendo 401 y 403 sin causa evidente.

**P7.7 ¿Existe script docker-compose o Makefile que documente cómo re-exportar el realm?**
Respuesta: **NO**
Evidencia: `grep -rn "export\|kc.sh" infrastructure/docker-compose.yml infrastructure/README.md` →
cero coincidencias · `ls infrastructure/Makefile` → no existe
Detalle: No hay ningún mecanismo, automatizado ni documentado, para exportar el realm después de
cambios por consola. El único script de infraestructura es
`infrastructure/postgres/init/01-create-keycloak-db.sh` (acta A31), que crea la base de Keycloak.
**Este vacío es la causa estructural del GAP 7**, no un descuido puntual.

## GAP 8 · AccountLockedException definida pero nunca lanzada

**P8.1 ¿Dónde está definida `AccountLockedException`?**
Respuesta: SÍ
Evidencia: `src/Cauce.Domain/Identity/Exceptions/AccountLockedException.cs:9-25`

```csharp
public sealed class AccountLockedException : DomainException
{
    /// <summary>
    /// Momento, en UTC, hasta el cual la cuenta permanece bloqueada.
    /// </summary>
    public DateTime LockedUntil { get; }

    public AccountLockedException(DateTime lockedUntil)
        : base("La cuenta se encuentra bloqueada temporalmente por intentos fallidos consecutivos.")
    {
        LockedUntil = lockedUntil;
    }
}
```

Detalle: Ya expone `LockedUntil`, que es justo el dato que US05 CA02 exige mostrar al usuario. La pieza
de dominio está lista; falta cablearla.

**P8.2 Contar throw sites de `AccountLockedException` en `src/` (excluir `tests/`)**
Respuesta: **CERO**
Evidencia: `grep -rn "throw new AccountLockedException" src/` → **0 resultados**
Detalle: La única referencia al tipo en todo `src/` es el mapeo a HTTP:

`src/Cauce.Api/Middleware/ExceptionHandlingMiddleware.cs:163-164`
```csharp
AccountLockedException => (
    StatusCodes.Status423Locked, "Cuenta bloqueada", "account_locked", exception.Message),
```

**Es código muerto**: hay traducción a 423 pero nadie produce la excepción. En `tests/` también son 0.

**P8.3 ¿Existe `User.RegisterFailedLogin()`? ¿Cuántos callers en `src/`?**
Respuesta: SÍ existe / **CERO callers en `src/`**
Evidencia: `src/Cauce.Domain/Identity/User.cs:218-227`

```csharp
public void RegisterFailedLogin(DateTime utcNow)
{
    FailedLoginAttempts++;
    if (FailedLoginAttempts >= MaxFailedLoginAttempts)
    {
        Lock(utcNow + LockDuration);
    }

    UpdatedAt = utcNow;
}
```

Detalle: `MaxFailedLoginAttempts = 5` (`User.cs:14`). Los únicos llamadores están en **tests de
dominio**: `tests/Cauce.Domain.Tests/Identity/UserTests.cs:72` y `:86`. El método está probado
unitariamente y funciona — simplemente **producción nunca lo invoca**.

**P8.4 ¿Existen `User.Lock()`, `User.IsLocked()`, `User.Unlock()`? ¿Callers en `src/`?**
Respuesta: SÍ existen / **CERO callers efectivos en `src/`**
Evidencia:

| Método | Definición | Callers en `src/` |
|---|---|---|
| `Lock(DateTime until)` | `User.cs:234-243` | Solo desde `RegisterFailedLogin` (`User.cs:223`), que nunca corre |
| `IsLocked(DateTime utcNow)` | `User.cs:315-318` | **0** |
| `Unlock()` | `User.cs:248-253` | **0** |
| `RegisterSuccessfulLogin(DateTime)` | `User.cs:206-211` | **1** — `LoginCommandHandler.cs:45` |

Detalle: De los cuatro métodos del ciclo de bloqueo, **solo el del camino feliz se usa**. Y
`RegisterSuccessfulLogin` resetea `FailedLoginAttempts = 0` y `LockedUntil = null`
(`User.cs:209-210`), o sea que el contador se limpia pero nunca se incrementa.

**P8.5 ¿El `LoginCommandHandler` verifica lockout antes de autenticar contra Keycloak?**
Respuesta: **NO**
Evidencia: `src/Cauce.Application/Identity/UseCases/Login/LoginCommandHandler.cs:36-56`

```csharp
public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
{
    var token = await _tokenClient
        .LoginAsync(request.Email, request.Password, request.ClientId, cancellationToken)
        .ConfigureAwait(false);

    var user = await _userRepository.FindByEmailAsync(request.Email, cancellationToken).ConfigureAwait(false);
    if (user is not null)
    {
        user.RegisterSuccessfulLogin(DateTime.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Login succeeded for user {UserId}.", user.Id);
    }

    return new LoginResult(
        token.AccessToken,
        token.RefreshToken,
        token.ExpiresIn,
        token.RefreshExpiresIn,
        token.TokenType);
}
```

Detalle: El handler completo son 20 líneas. **Va directo a Keycloak sin consultar el estado local.** No
llama a `IsLocked`, no llama a `RegisterFailedLogin`, y el `catch` de credenciales inválidas no existe
(la excepción se propaga desde `KeycloakTokenClient.cs:64`). El orden además está invertido respecto de
lo que pediría un lockout local: consulta al usuario **después** de autenticar.

**P8.6 ¿Existen columnas `locked_until`, `failed_login_attempts`, `last_failed_login_at` en `users`?**
Respuesta: PARCIAL
Evidencia: `src/Cauce.Infrastructure/Persistence/Configurations/UserConfiguration.cs:63, 67` ·
`src/Cauce.Infrastructure/Persistence/Migrations/20260624212001_AddIdentityTables.Designer.cs:273, 293`

| Columna | Existe | Evidencia |
|---|---|---|
| `failed_login_attempts` | **SÍ** | `UserConfiguration.cs:63` (`int`, presente desde la migración inicial) |
| `locked_until` | **SÍ** | `UserConfiguration.cs:67` (`DateTime?`) |
| `last_failed_login_at` | **NO** | No aparece en ninguna configuración ni migración |

Detalle: **El esquema ya soporta el lockout.** Las dos columnas necesarias existen desde el 24 de junio
y se persisten correctamente. Solo falta lógica que las escriba. `last_failed_login_at` no existe, pero
tampoco hace falta para el algoritmo actual (que usa contador + `locked_until`); si se quisiera, exige
migración nueva.

**P8.7 ¿La API declara 423 (Locked) en algún endpoint de auth?**
Respuesta: **NO** (en los contratos) / **SÍ** (en el middleware)
Evidencia: `src/Cauce.Api/Controllers/AuthController.cs:86-89` ·
`backend/docs/api/openapi-v1.0.0.json` (path `/api/v1/auth/login`) ·
`src/Cauce.Api/Middleware/ExceptionHandlingMiddleware.cs:163-164`
Detalle: Hay una **inconsistencia entre el middleware y el contrato publicado**:

- El middleware **sí** mapea `AccountLockedException` → 423 `account_locked`.
- `AuthController.Login` declara solo `[ProducesResponseType]` de 200, 400, 401, 429 y 500.
  **No declara 423.**
- El OpenAPI generado (`docs/api/openapi-v1.0.0.json`) lista para `/auth/login` únicamente 200, 400,
  401, 429, 500. **423 no aparece en ningún endpoint del contrato.**

Un cliente generado desde el contrato no sabría manejar un 423 si algún día se lanzara.

---

# FASE 2 · Contratos y estructura base

## 2.A Endpoints y controllers

**P9.1 Listar todos los archivos bajo `src/Cauce.Api/Controllers/`**
Respuesta: SÍ — 22 archivos (21 controllers + 1 base)
Evidencia: `grep -rn "^\[Route" src/Cauce.Api/Controllers/`

| Archivo | Ruta base | Responsabilidad |
|---|---|---|
| `BaseApiController.cs` | `api/v{version}/[controller]` | Clase base con `[ApiController]`; no expone endpoints |
| `AdminController.cs` | `api/v{version}/admin` | Provisión de nutricionistas (`[AdminApiKey]`) |
| `AllergiesController.cs` | `api/v{version}/allergies` | Catálogo de alergias |
| `AuthController.cs` | `api/v{version}/auth` | Registro, login, logout, password reset |
| `ClinicalNotesController.cs` | `api/v{version}/clinical-notes` | Notas clínicas del paciente |
| `CustomFoodsController.cs` | `api/v{version}/custom-foods` | Alimentos personalizados (CRUD) |
| `FoodsController.cs` | `api/v{version}/foods` | Catálogo de alimentos, búsqueda, sugerencias |
| `GlossaryController.cs` | `api/v{version}/glossary` | Glosario clínico y búsqueda |
| `HealthController.cs` | `api/v{version}/health` | Health check |
| `HistoryController.cs` | `api/v{version}/history` | Historial unificado del paciente |
| `IbsSssController.cs` | `api/v{version}/ibs-sss` | Evaluaciones IBS-SSS, evolución, última |
| `InvitationsController.cs` | `api/v{version}/invitations` | Generación de códigos de invitación |
| `MealsController.cs` | `api/v{version}/meals` | Registro e historial de comidas |
| `NutritionistsController.cs` | `api/v{version}/nutritionists` | Panel de triaje, detalle y evolución de pacientes |
| `PatientsController.cs` | `api/v{version}/patients` | Perfil, alergias, export, consent PDF, baja, summary, reporte |
| `SymptomsController.cs` | `api/v{version}/symptoms` | Registro e historial de síntomas |
| `SyncController.cs` | `api/v{version}/sync` | Sincronización por lotes offline |
| `UsersController.cs` | `api/v{version}/users` | **Solo** `PUT me/fcm-token` |
| `Recommendations/RecommendationsController.cs` | `api/v{version}/recommendations` | Detalle de recomendación (ambos roles) |
| `Recommendations/PatientRecommendationsController.cs` | `api/v{version}/recommendations` | Generar, listar, entregar, feedback |
| `Recommendations/NutritionistRecommendationsController.cs` | `api/v{version}/recommendations` | Pending review, approve, reject, modify, manual, archive |
| `Reports/ReportsController.cs` | `api/v{version}/reports` | Reporte clínico por paciente |

**P9.2 Listar todos los endpoints bajo `/api/v1/auth/*`**
Respuesta: SÍ — 5 endpoints
Evidencia: `src/Cauce.Api/Controllers/AuthController.cs`

| Método + ruta | Acción del controller | Command MediatR | Línea |
|---|---|---|---|
| `POST /api/v1/auth/register` | `Register` | `RegisterPatientCommand` | 43 |
| `POST /api/v1/auth/login` | `Login` | `LoginCommand` | 84 |
| `POST /api/v1/auth/logout` | `Logout` | `LogoutCommand` | 103 |
| `POST /api/v1/auth/password-reset/request` | `RequestPasswordReset` | `RequestPasswordResetCommand` | 120 |
| `POST /api/v1/auth/password-reset/confirm` | `ConfirmPasswordReset` | `ConfirmPasswordResetCommand` | 144 |

Rate limits: `auth-register` 5/h·IP · `auth-login` 10/min·IP · `auth-pwreset` 3/h·IP (las dos de reset
comparten política). `logout` no tiene política.
Evidencia de límites: `src/Cauce.Api/Configuration/RateLimitingPolicies.cs:53-57`.

**P9.3 ¿Existe `IKeycloakClient`, `KeycloakService` o servicio abstracto para hablar con Keycloak?**
Respuesta: SÍ — **dos** interfaces separadas
Evidencia: `src/Cauce.Application/Common/Interfaces/Identity/IKeycloakTokenClient.cs:8-29` ·
`src/Cauce.Application/Common/Interfaces/Identity/IKeycloakAdminClient.cs:8-95`
Detalle: Nota — viven en `Cauce.Application/Common/Interfaces/Identity/`, no en
`src/Cauce.Infrastructure/Auth/` como supone el prompt (regla de dependencia de Clean Architecture:
interfaz en Application, implementación en Infrastructure).

Interfaz completa de tokens:

```csharp
public interface IKeycloakTokenClient
{
    Task<KeycloakTokenResult> LoginAsync(string email, string password, string clientId, CancellationToken ct = default);

    Task LogoutAsync(string refreshToken, string clientId, CancellationToken ct = default);
}

public sealed record KeycloakTokenResult(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    int RefreshExpiresIn,
    string TokenType);
```

Implementación: `src/Cauce.Infrastructure/Identity/KeycloakTokenClient.cs`.

`IKeycloakAdminClient` (siete métodos, para el Admin API con service account):
`CreateUserAsync`, `SetTemporaryPasswordAsync`, `ResetPasswordAsync`, `SendVerifyEmailAsync`,
`DeleteUserAsync`, `DisableUserAsync`, `FindByEmailAsync`.
Implementación: `src/Cauce.Infrastructure/Identity/KeycloakAdminClient.cs`.

**P9.4 ¿Qué método invoca `/token` (login inicial) en Keycloak? Copiar la firma.**
Respuesta: SÍ
Evidencia: `src/Cauce.Infrastructure/Identity/KeycloakTokenClient.cs:43`

```csharp
public async Task<KeycloakTokenResult> LoginAsync(
    string email, string password, string clientId, CancellationToken ct = default)
```

URL: `{Authority}/protocol/openid-connect/token` (`KeycloakTokenClient.cs:38`).
Form enviado (líneas 45-52): `grant_type=password`, `client_id`, `username`, `password`,
`scope=openid`. **No envía `client_secret`.**

**P9.5 ¿Existe algún método para invocar `/token` con `grant_type=refresh_token`?**
Respuesta: **NO**
Evidencia: `IKeycloakTokenClient.cs:8-29` declara únicamente `LoginAsync` y `LogoutAsync`
Detalle: Es la pieza que hay que agregar para el GAP 1. La URL del token endpoint ya está resuelta en
la propiedad privada `TokenUrl` (`KeycloakTokenClient.cs:38`) y se puede reutilizar.

**P9.6 ¿Qué método invoca `/logout` en Keycloak?**
Respuesta: SÍ
Evidencia: `src/Cauce.Infrastructure/Identity/KeycloakTokenClient.cs:93`

```csharp
public async Task LogoutAsync(string refreshToken, string clientId, CancellationToken ct = default)
```

URL: `{Authority}/protocol/openid-connect/logout` (`KeycloakTokenClient.cs:40`).
Form: `client_id`, `refresh_token`. Es **best-effort**: si Keycloak responde error, se registra un
warning y no se propaga (`KeycloakTokenClient.cs:108-115`).

## 2.B Patrones del proyecto

**P10.1 Patrón para DTOs de request/response**
Respuesta: SÍ — `sealed record` posicional, **sin `[JsonPropertyName]`**, casing por convención
Evidencia: `src/Cauce.Api/Contracts/Identity/RegisterPatientRequest.cs:40-46` ·
`src/Cauce.Application/Identity/UseCases/RegisterPatient/RegisterPatientResult.cs:60-64`

```csharp
// Request — capa Api (Cauce.Api/Contracts/<Modulo>/)
public sealed record RegisterPatientRequest(
    string Email,
    string FullName,
    string Password,
    string ConsentDocumentVersion,
    string ConsentTextHash,
    string? InvitationCode);

// Response — capa Application (Cauce.Application/<Modulo>/UseCases/<Caso>/)
public sealed record RegisterPatientResult(
    Guid UserId,
    string Email,
    UserStatus Status,
    bool EmailVerificationRequired);
```

Detalle del patrón, relevante para escribir el fix:
- Los **requests** viven en `Cauce.Api/Contracts/<Modulo>/`; los **results** viven junto a su caso de
  uso en `Cauce.Application/`. Asimetría deliberada.
- **Ningún DTO usa `[JsonPropertyName]`.** El camelCase sale de `Program.cs:131`.
- Todos son `sealed record` posicionales con XML doc en español por parámetro.
- Los enums se serializan por nombre PascalCase (`JsonStringEnumConverter` sin naming policy,
  `Program.cs:132`).

**P10.2 ¿Los ProblemDetails custom tienen un formato conocido para `errors`?**
Respuesta: SÍ
Evidencia: `src/Cauce.Api/Middleware/ExceptionHandlingMiddleware.cs:271-301`
Detalle: Hay **dos formas** de error, y un cliente debe manejar las dos.

**(a) Error general** (`BuildProblem`, líneas 290-301) — sin `errors`:

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

**(b) Error de validación** (`BuildValidationProblem`, líneas 271-288) — con `errors`:

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
  "traceId": "00-...",
  "errorCode": "validation_error"
}
```

Observaciones vinculantes:
- `type` e `instance` **nunca se asignan**: siempre llegan `null`.
- `errorCode` es la extensión sobre la que el cliente debe hacer switch.
- **Las claves de `errors` van en PascalCase** (GAP 3).
- El **429 no lleva `errorCode`**: `RateLimitingPolicies.cs:71-77` construye su propio `ProblemDetails`
  con `status`, `title`, `detail` y `retryAfterSeconds`, más el header `Retry-After`.
- El 400 de binding de `[ApiController]` (JSON malformado, campo obligatorio ausente) produce el
  `ValidationProblemDetails` automático de MVC, que **tampoco lleva `errorCode`**.

**P10.3 Patrón de tests de integración con Testcontainers**
Respuesta: SÍ
Evidencia: `tests/Cauce.Api.IntegrationTests/Identity/Support/PostgresFixture.cs:12-52`

Fixture completa:

```csharp
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public bool IsAvailable { get; private set; }

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .Build();
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
            IsAvailable = true;
        }
        catch (Exception)
        {
            IsAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
```

Test corto sobre un endpoint autenticado (`AuditMiddlewareTests.cs:63-79`):

```csharp
[SkippableFact]
public async Task Logout_Authenticated_WritesLogoutAuditWithActor()
{
    SkipIfUnavailable();
    var patient = await SeedPatientAsync();
    var client = PatientClient(patient.KeycloakId, patient.Email);

    var response = await client.PostAsJsonAsync("/api/v1/auth/logout", new
    {
        refreshToken = "fake-refresh-token",
        clientId = "cauce-mobile"
    });
    response.StatusCode.Should().Be(HttpStatusCode.NoContent);

    var log = (await AuditLogsAsync(action: AuditActionType.Logout)).Should().ContainSingle().Subject;
    log.ActorUserId.Should().Be(patient.Id);
}
```

Piezas del andamiaje, en `tests/Cauce.Api.IntegrationTests/Identity/Support/`:
`CustomWebApplicationFactory.cs`, `Prompt5IntegrationTestBase.cs` (base con `SkipIfUnavailable`,
`SeedPatientAsync`, `PatientClient`, `AuditLogsAsync`), `TestJwtBuilder.cs` (firma JWT locales con
`aud`/`sub`/rol), `DatabaseReset.cs`, y fixtures `PostgresFixture`, `RedisFixture`, `MailpitFixture`,
`MinioFixture`. Dobles: `FakeKeycloakTokenClient`, `FakeKeycloakAdminClient`, `FakeEmailSender`.
Todos los tests llevan `[Trait("Category", "Integration")]` y `[SkippableFact]`.

**P10.4 ¿El proyecto usa MediatR? ¿Cada endpoint tiene Command/Query con Handler separado?**
Respuesta: SÍ a ambas
Evidencia: `src/Cauce.Api/Controllers/AuthController.cs:23` (`ISender _mediator`) ·
`src/Cauce.Application/Identity/UseCases/` (28 archivos organizados por caso de uso)
Detalle: Patrón consistente. Cada caso de uso vive en su propia carpeta
`Cauce.Application/<Modulo>/UseCases/<NombreCaso>/` con hasta cuatro archivos:
`<Nombre>Command.cs` (que además contiene el `record` de resultado), `<Nombre>CommandHandler.cs`,
`<Nombre>CommandValidator.cs` y a veces `<Nombre>Result.cs` separado. Los controllers son delgados:
mapean el request de contrato al command y lo envían.

**P10.5 ¿Existe `ValidationBehavior<TRequest, TResponse>`? ¿Cómo se registran los validators?**
Respuesta: SÍ
Evidencia: `src/Cauce.Application/Common/Behaviors/ValidationBehavior.cs:14-57`

```csharp
public async Task<TResponse> Handle(
    TRequest request,
    RequestHandlerDelegate<TResponse> next,
    CancellationToken cancellationToken)
{
    if (!_validators.Any())
    {
        return await next().ConfigureAwait(false);
    }

    var context = new ValidationContext<TRequest>(request);

    var results = await Task.WhenAll(
        _validators.Select(validator => validator.ValidateAsync(context, cancellationToken)))
        .ConfigureAwait(false);

    var failures = results
        .SelectMany(result => result.Errors)
        .Where(failure => failure is not null)
        .ToList();

    if (failures.Count > 0)
    {
        throw new ValidationException(failures);
    }

    return await next().ConfigureAwait(false);
}
```

Detalle: Los validators se inyectan como `IEnumerable<IValidator<TRequest>>`. Hay **cuatro behaviors**
en el pipeline: `ValidationBehavior`, `AuditingBehavior`, `IdempotencyBehavior` y `LoggingBehavior`
(`src/Cauce.Application/Common/Behaviors/`). El registro concreto vive en
`src/Cauce.Application/DependencyInjection.cs` (`AddApplication()`, llamado desde `Program.cs:37`).

## 2.C Documentos previos

**P11.1 ¿Existe `mobile/docs/CONTRACT-IDENTITY-v1.md`?**
Respuesta: PARCIAL — existe, pero **no en el mobile**
Evidencia: `backend/docs/api/CONTRACT-IDENTITY-v1.md` (587 líneas)
Detalle: **Está en el repo `backend`, no en `mobile`, y está SIN TRACKEAR** (`git status` →
`?? docs/api/CONTRACT-IDENTITY-v1.md`). Fechado el 13 de julio de 2026, v1.0, levantado sobre el tag
`v0.6.2-dev-seed`. Índice de secciones:

1. Tabla resumen (7 endpoints + tabla de "endpoints que NO existen")
2. Endpoints (2.1 register · 2.2 login · 2.3 logout · 2.4 password-reset/request ·
   2.5 password-reset/confirm · 2.6 consent/pdf · 2.7 fcm-token)
3. Envelope de error (RFC 7807)
4. Errores de validación por campo (US01 CA02) — 4.1 FluentValidation · 4.2 binding de `[ApiController]`
5. Ciclo de vida del token
6. Bloqueo de cuenta por intentos fallidos
7. Verificación de correo
8. Consentimiento informado (8.1 origen · 8.2 cálculo del hash · 8.3 verificación · 8.4 lo que se
   persiste · 8.5 consecuencia para el móvil)
9. Rutas necesarias para US01, US05, US07 y US08
10. Historial

**Este documento confirma de forma independiente los 8 gaps.** Su análisis coincide con el de esta
verificación en todos los puntos comprobados.

**P11.2 ¿Existe `mobile/docs/MATRIZ-IDENTIDAD.md`?**
Respuesta: PARCIAL — existe, pero **no en el mobile**
Evidencia: `docs/traceability/MATRIZ-IDENTIDAD.md` (repo `docs`)
Detalle: **SIN TRACKEAR** (`git status` del repo `docs` → `?? traceability/MATRIZ-IDENTIDAD.md`).
Fechado el 13 de julio de 2026. Cubre **15 CAs** (el prompt original decía 14):

| CA | Estado declarado |
|---|---|
| US01-CA01 | PARCIAL |
| US01-CA02 | PARCIAL |
| US01-CA03 | **ROJO** |
| US01-CA04 | PARCIAL |
| US05-CA01 | PARCIAL |
| US05-CA02 | PARCIAL |
| US07-CA01 | PARCIAL |
| US07-CA02 | PARCIAL |
| US08-CA01 | PARCIAL |
| US08-CA02 | **ROJO** |
| TS01-CA01 | PARCIAL |
| TS01-CA02 | **VERDE** |
| TS05-CA01 | **ROJO** |
| TS05-CA02 | PARCIAL |
| US20-CA02 | PARCIAL |

Recuento: 1 verde, 11 parciales, 3 rojos. El documento incluye además una sección "Brechas de backend
que bloquean Mobile-1b" con 5 entradas, que se corresponden con los gaps 4, 7, 8, 1 y 2 de este
reporte, y una reconciliación que contradice al `anexo-oe3-cobertura-ca.md` (que declaraba US05 CA02 y
TS01 CA02 al 100 %).

**P11.3 ¿Los prompts `PROMPT-VERIFICACION-PRE-MOBILE-1B.md` y
`PROMPT-VERIFICACION-02-CONTRATO-IDENTIDAD.md` están versionados?**
Respuesta: **NO ENCONTRADO**
Evidencia: `find . -iname "*VERIFICACION*"` sobre los cinco repos → cero resultados
Detalle: No existen en disco ni en git, en ningún repo. Se ejecutaron y se descartaron. Solo
sobrevivieron sus salidas (`CONTRACT-IDENTITY-v1.md` y `MATRIZ-IDENTIDAD.md`), y ambas sin commitear.

**P11.4 ¿Existe alguna acta M9 o posterior en `mobile/docs/DECISIONS-BLOCK-MOBILE-1.md`?**
Respuesta: **NO**
Evidencia: `mobile/DECISIONS-BLOCK-MOBILE-1.md` (en la **raíz** del repo, no en `docs/`)
Detalle: El archivo declara "Actas M1 a M8" y contiene exactamente esas ocho:

| Acta | Decisión |
|---|---|
| M1 | State management con Riverpod codegen |
| M2 | Arquitectura feature-first con capas data/domain/presentation |
| M3 | Routing con go_router 14+ |
| M4 | HTTP con dio y cliente OpenAPI generado |
| **M5** | **Auth con `flutter_appauth` y PKCE directo a Keycloak** |
| M6 | Almacenamiento seguro de tokens con `flutter_secure_storage` |
| M7 | Persistencia local con drift 2.20+ sobre sqflite |
| M8 | Localización con arb files y default `es_PE` |

**No hay M9.** `mobile/docs/` contiene solo `decisions/.gitkeep`.

⚠️ **Contradicción documental sin resolver.** El acta **M5 sigue vigente en el repo del mobile** y
declara PKCE directo contra Keycloak. La `MATRIZ-IDENTIDAD.md` la da por **anulada**:

> "El acta M5 del mobile (Authorization Code + PKCE directo contra Keycloak) queda anulada. Motivo: el
> `AuditingMiddleware` detecta el login solo por la ruta `POST /auth/login` (…) Con PKCE directo,
> `audit_logs` quedaría sin registro de accesos y se incumpliría la Ley N° 29733."

Pero la matriz **no está commiteada** y **no existe un acta M9 que formalice la anulación**. Hoy la
fuente de verdad versionada del mobile dice PKCE directo. Ver Bloqueantes.

**P11.5 ¿Existe `mobile/docs/acceptance-criteria-matrix.md` o similar del lado mobile?**
Respuesta: **NO** (del lado mobile) / SÍ (en el repo `docs`, compartido)
Evidencia: `docs/traceability/acceptance-criteria-matrix.md` (2026-07-06, commiteado) ·
`docs/traceability/anexo-oe3-cobertura-ca.md` (2026-07-06, commiteado) ·
`docs/traceability/MATRIZ-IDENTIDAD.md` (2026-07-13, **sin commitear**)
Detalle: No hay matriz de CA propia del mobile. Las tres viven en el repo `docs` compartido.

---

# FASE 3 · Estado del mobile a hoy

**P12.1 Dependencias de `mobile/pubspec.yaml` con versión pineada**
Respuesta: SÍ
Evidencia: `mobile/pubspec.yaml:10-60`
Detalle: Todas usan rango caret (`^`), ninguna está pineada a versión exacta.
SDK: `sdk: ^3.5.0`, `flutter: ">=3.24.0"`.

```
# dependencies
flutter:                  sdk
flutter_localizations:    sdk
cupertino_icons:          ^1.0.8
flutter_riverpod:         ^2.5.1
riverpod_annotation:      ^2.3.5
freezed_annotation:       ^2.4.4
json_annotation:          ^4.9.0
dio:                      ^5.7.0
flutter_appauth:          ^8.0.1
flutter_secure_storage:   ^9.2.2
drift:                    ^2.20.0
sqlite3_flutter_libs:     ^0.5.24
path_provider:            ^2.1.4
path:                     ^1.9.0
go_router:                ^14.2.7
uuid:                     ^4.5.0
intl:                     ^0.20.2
flutter_dotenv:           ^5.2.1
connectivity_plus:        ^6.0.5

# dev_dependencies
flutter_test:             sdk
flutter_lints:            ^5.0.0
build_runner:             ^2.4.13
riverpod_generator:       ^2.4.3
freezed:                  ^2.5.7
json_serializable:        ^6.8.0
drift_dev:                ^2.20.3
mocktail:                 ^1.0.4
```

Bandera de `flutter:` → `uses-material-design: true`, `generate: true`.

**P12.2 ¿Existe `mobile/lib/features/auth/`? Listar árbol hasta 2 niveles.**
Respuesta: PARCIAL — la carpeta existe, **está vacía**
Evidencia: `find mobile/lib -type f`

```
lib/
├── app.dart
├── main.dart
├── core/
│   ├── auth/.gitkeep
│   ├── config/.gitkeep
│   ├── errors/.gitkeep
│   ├── network/.gitkeep
│   ├── routing/.gitkeep
│   ├── storage/.gitkeep
│   ├── theme/.gitkeep
│   └── utils/.gitkeep
├── features/
│   ├── auth/
│   │   ├── data/.gitkeep
│   │   ├── domain/.gitkeep
│   │   └── presentation/.gitkeep
│   ├── shared/widgets/.gitkeep
│   └── splash/.gitkeep
└── l10n/.gitkeep
```

Detalle crítico: **todo `lib/` son marcadores `.gitkeep`.** Los únicos archivos Dart de producción son
`lib/app.dart` y `lib/main.dart`. Mobile-1a entregó **estructura de carpetas y declaración de
dependencias, cero implementación**. Las pantallas de login/register del prototipo previo
(commits `2bb7d39`, `69523cc`, `e4a6ba0`) se **borraron** en el reset de scaffold (`24710fa`).

**P12.3 ¿Existe `mobile/lib/core/theme/` con `AppColors`, `AppTypography`, `AppSpacing`?**
Respuesta: PARCIAL — la carpeta existe, **sin archivos**
Evidencia: `lib/core/theme/.gitkeep` es su único contenido
Detalle: `AppColors`, `AppTypography` y `AppSpacing` → **NO ENCONTRADO**.

**P12.4 ¿Existe `mobile/lib/core/network/` con setup de dio?**
Respuesta: PARCIAL — la carpeta existe, **sin archivos**
Evidencia: `lib/core/network/.gitkeep` es su único contenido
Detalle: No hay cliente dio, ni interceptors, ni configuración de base URL. `dio: ^5.7.0` está
declarado en `pubspec.yaml` pero no se usa en ninguna línea de código.

**P12.5 ¿Existe `mobile/lib/core/storage/` con setup de drift? ¿Qué schema versión?**
Respuesta: PARCIAL — la carpeta existe, **sin archivos**
Evidencia: `lib/core/storage/.gitkeep` es su único contenido
Detalle: **No hay esquema drift, ni tablas, ni DAOs, ni `schemaVersion`.** `drift: ^2.20.0` y
`drift_dev: ^2.20.3` están declarados pero sin usar. Coherente con TS05-CA01 = ROJO en la matriz.

**P12.6 ¿`pubspec.lock` existe y es consistente con `pubspec.yaml`?**
Respuesta: SÍ (existe y resuelve) — con **desactualización importante**
Evidencia: `mobile/pubspec.lock` (30 607 bytes, 2026-07-11) · `flutter pub outdated --show-all`
(exit 0, sin errores de resolución)
Detalle: El lock es consistente y resuelve limpio. Pero muchas dependencias directas quedaron atrás,
varias con salto de major:

| Paquete | Resuelto hoy | Última | Salto |
|---|---|---|---|
| `flutter_appauth` | 8.0.3 | **12.0.2** | 4 majors |
| `go_router` | 14.8.1 | **17.5.0** | 3 majors |
| `flutter_secure_storage` | 9.2.4 | **11.0.0** | 2 majors |
| `flutter_riverpod` | 2.6.1 | **3.4.2** | 1 major |
| `riverpod_annotation` | 2.6.1 | **4.0.6** | 2 majors |
| `freezed` | 2.5.8 | **3.2.5** | 1 major |
| `freezed_annotation` | 2.4.4 | **3.1.0** | 1 major |
| `drift` | 2.28.2 | 2.34.3 | minor |
| `connectivity_plus` | 6.1.5 | **7.3.1** | 1 major |
| `flutter_dotenv` | 5.2.1 | **6.0.1** | 1 major |
| `flutter_lints` | 5.0.0 | **6.0.0** | 1 major |
| `sqlite3_flutter_libs` | 0.5.42 | **0.6.0+eol** | marcado **EOL** |

Nota: `flutter_appauth` existía para el acta M5 (PKCE directo). Si M5 queda anulada, esa dependencia
podría sobrar. `sqlite3_flutter_libs` marcado `+eol` merece atención antes de construir la capa drift.

**P12.7 ¿Existe algún cliente OpenAPI generado en `mobile/lib/api/`?**
Respuesta: **NO**
Evidencia: `find mobile/lib -type f` no muestra ninguna carpeta `api/`
Detalle: El contrato **está importado** como snapshot crudo en `mobile/openapi/openapi-v1.0.0.json`
(commit `42a5d2e`, "import backend contract v1.0.0 snapshot"), pero **no se generó ningún cliente a
partir de él**. El acta M4 declara la intención ("cliente OpenAPI generado"); la generación no se
ejecutó. Tampoco hay configuración de generador (`openapi-generator`, `swagger_dart_code_generator`) en
`pubspec.yaml`.

**P12.8 ¿Existe `mobile/l10n.yaml` o setup de l10n? ¿Cuántos archivos `.arb`?**
Respuesta: **NO**
Evidencia: `ls mobile/l10n.yaml` → no existe · `find . -name "*.arb"` (excluyendo `build/` y
`.dart_tool/`) → cero archivos
Detalle: `lib/l10n/` existe pero solo contiene `.gitkeep`. `flutter_localizations` está declarado y
`pubspec.yaml:64` declara `generate: true`.

⚠️ **Inconsistencia latente:** `generate: true` sin `l10n.yaml` ni archivos `.arb` significa que la
generación de localizaciones no produce nada. El acta M8 (arb + `es_PE`) está declarada pero no
implementada.

---

# FASE 4 · Riesgo de regresión al fixar

**P13.1 Ejecutar `dotnet test` con filtro de Auth. ¿Cuántos tests corren? ¿Todos verdes?**
Respuesta: PARCIAL — **49 verdes, 24 omitidos, 0 rojos**
Evidencia: `dotnet test Cauce.sln --filter "FullyQualifiedName~Auth|FullyQualifiedName~Login|FullyQualifiedName~Identity"`

| Proyecto | Pasados | Fallidos | Omitidos | Total | Duración |
|---|---|---|---|---|---|
| `Cauce.Domain.Tests` | 28 | 0 | 0 | 28 | 182 ms |
| `Cauce.Application.Tests` | 20 | 0 | 0 | 20 | 566 ms |
| `Cauce.Infrastructure.Tests` | 1 | 0 | 0 | 1 | 1 s |
| `Cauce.Api.IntegrationTests` | 0 | 0 | **24** | 24 | 60 ms |
| **Total** | **49** | **0** | **24** | **73** | |

Detalle: **Ningún test falla.** La solución compila limpia. Los 24 tests de integración se **omiten**
porque Docker no responde (P0.13) y `PostgresFixture.IsAvailable` queda en `false`, activando
`SkipIfUnavailable()`. Esto significa que **la cobertura de integración de identidad no se verificó en
esta corrida**; en un entorno con Docker arriba deberían pasar, pero no se pudo comprobar.

**P13.2 ¿Existen tests que asumen que `/auth/login` retorna SOLO tokens? Si arreglamos el GAP 5,
¿romperán?**
Respuesta: **NO** — ningún test rompería
Evidencia: `grep -rn "accessToken\|AccessToken\|refreshToken\|LoginResult" tests/ --include=*.cs` → 3
coincidencias, ninguna es una aserción sobre el cuerpo de la respuesta de login
Detalle: Las tres coincidencias son:
- `AuditMiddlewareTests.cs:72` — un `refreshToken` en el **request** de logout, no en una respuesta.
- `FakeKeycloakTokenClient.cs:32` — construcción del doble.
- `FakeKeycloakTokenClient.cs:40` — parámetro de `LogoutAsync`.

Los tres tests que llaman a `/auth/login` (`AuditMiddlewareTests.cs:26, 45, 64`) solo assertean
`response.StatusCode` y las filas de `audit_logs`. **Agregar campos a `LoginResult` no rompe nada.**

**P13.3 ¿Existen tests que asuman `errors` en PascalCase? Si arreglamos el GAP 3, ¿romperán?**
Respuesta: **NO** — ningún test rompería
Evidencia: `grep -rn "ProblemDetails\|\"errors\"\|errorCode\|ValidationProblem" tests/ --include=*.cs`
→ **cero coincidencias**
Detalle: **No existe ni un solo test en todo el repo que assertee sobre el envelope de error.**
Cambiar el casing de las claves de `errors` es libre de regresión desde el punto de vista de la suite.
El corolario incómodo: tampoco hay red de seguridad para verificar que el fix quedó bien, así que el
fix debería traer su propio test.

**P13.4 ¿Algún consumidor externo documentado del backend?**
Respuesta: PARCIAL — solo un artefacto muerto
Evidencia: `src/Cauce.Api/Cauce.Api.http`

```
@Cauce.Api_HostAddress = http://localhost:5008

GET {{Cauce.Api_HostAddress}}/weatherforecast/
Accept: application/json

###
```

Detalle: Es el **scaffold por defecto de Visual Studio**, sin tocar: apunta al puerto 5008 (la API
corre en 5074) y a `/weatherforecast/`, que nunca existió en este proyecto. **No es un consumidor
real.** No hay colección Postman, ni script bash de smoke test, ni cliente HTTP en `docs/`.

Consumidores reales documentados:
- `mobile/openapi/openapi-v1.0.0.json` — snapshot del contrato, sin cliente generado (P12.7).
- `backend/docs/api/openapi-v1.0.0.json` — contrato fuente, 57 operaciones.
- `backend/docs/api/ENDPOINTS.md` y `backend/docs/api/CONTRACT-IDENTITY-v1.md`.
- El smoke test del paciente demo descrito en `backend/docs/dev/SEED-USERS.md` fue manual, no un script.

**P13.5 ¿Existe un ambiente Testcontainers CI actualmente pasando? ¿Qué imagen de Keycloak usa?
¿Coincide con el docker-compose local?**
Respuesta: PARCIAL
Evidencia: `mobile/.github/workflows/ci.yml` · `infrastructure/docker-compose.yml:79`
Detalle:
- **CI del backend: NO EXISTE.** No hay `.github/workflows/` en el repo `backend`. El único workflow
  del proyecto es `mobile/.github/workflows/ci.yml` (commit `f522073`, "minimal analyze and test
  workflow"), que es de Flutter, no de .NET. **Nada ejecuta la suite de integración de forma
  automática.**
- **Imagen de Keycloak en Testcontainers: ninguna.** Los tests no levantan Keycloak; usan
  `FakeKeycloakTokenClient` (P6.4). La única imagen que Testcontainers usa es `postgres:16-alpine`
  (`PostgresFixture.cs:32`).
- **Comparación con docker-compose local:**

| Servicio | docker-compose | Testcontainers | ¿Coincide? |
|---|---|---|---|
| Postgres | `postgres:16-alpine` | `postgres:16-alpine` | **SÍ** |
| Keycloak | `quay.io/keycloak/keycloak:25.0` | *(no se levanta)* | **N/A** |
| KeyDB | `eqalpha/keydb:alpine` | `RedisFixture` (imagen no verificada en esta corrida) | parcial |
| MinIO | `minio/minio:latest` | `MinioFixture` | parcial |
| Mailpit | `axllent/mailpit:latest` | `MailpitFixture` | parcial |

La divergencia importante es Keycloak: **la versión real (25.0) nunca se ejercita en pruebas
automáticas.**

**P13.6 ¿El mobile ya llama a `/auth/login` desde algún test o setup? ¿Asume un shape específico?**
Respuesta: **NO**
Evidencia: `find mobile/test -type f` → `test/widget_test.dart` más tres `.gitkeep`
(`test/integration/`, `test/unit/`, `test/widget/`)
Detalle: El único test del mobile es `widget_test.dart`, el scaffold por defecto de Flutter. **El
mobile no hace ninguna llamada HTTP en ningún test ni en ningún setup**, y no tiene ni siquiera cliente
dio configurado (P12.4). **No asume ningún shape.** Cambiar el contrato de login hoy no rompe nada del
lado móvil.

---

# FASE 5 · Estado clínico y de datos

> Las cuatro preguntas de esta fase dependen de una base de datos accesible. **PostgreSQL no está
> corriendo** (puerto 5432 cerrado, P0.13), así que no se pudieron responder contra datos reales. Lo
> que sigue documenta lo que el **código** garantiza que se crearía al levantar el entorno.

**P14.1 ¿Cuántos usuarios existen en `users`? ¿Existe `paciente.demo@cauce.local`?**
Respuesta: NO VERIFICABLE
Evidencia: puerto 5432 CERRADO · `src/Cauce.Infrastructure/Persistence/Seeders/DemoPatientSeeder.cs` ·
`src/Cauce.Api/appsettings.Development.json` (sección `DemoPatient`)
Detalle: No se puede consultar el conteo. Lo que el código garantiza: al arrancar en Development,
`DemoPatientSeeder` provisiona `paciente.demo@cauce.local` (`FullName: "Paciente Demo Kaelín"`,
password `Paciente.Demo2026!`, permanente) de forma **idempotente** — no-op si ya existe por correo.
El seeder está habilitado (`DemoPatient:Enabled: true`).

**P14.2 ¿Existe rol Nutritionist seedeado con al menos un usuario para probar HITL?**
Respuesta: NO VERIFICABLE (datos) / SÍ (configuración)
Evidencia: `src/Cauce.Infrastructure/Persistence/Seeders/UserRolesSeeder.cs` ·
`src/Cauce.Infrastructure/Persistence/Seeders/DevAdminSeeder.cs` ·
`src/Cauce.Api/appsettings.Development.json` (sección `DevAdmin`) ·
`infrastructure/keycloak/import/realm.json:63-67`
Detalle: El rol `nutritionist` está declarado en el realm y `UserRolesSeeder` puebla el catálogo
`user_roles`. `DevAdminSeeder` provisiona `nutricionista.demo@cauce.local` (`Dietista Demo`, password
temporal `Demo123!`).

⚠️ **Limitación conocida:** ese usuario **no puede autenticarse por Direct Access Grant** porque queda
con la acción requerida `UPDATE_PASSWORD` pendiente (documentado en `backend/docs/dev/SEED-USERS.md` y
en `backend/CLAUDE.md`). Para probar HITL end-to-end hay que cambiarle la contraseña primero desde la
consola de Keycloak.

**P14.3 ¿La tabla `audit_logs` tiene filas? ¿De qué fechas?**
Respuesta: NO VERIFICABLE
Evidencia: puerto 5432 CERRADO
Detalle: No se puede consultar. No hay seeder que popule `audit_logs`: se llena solo por actividad real
(middleware, behavior, `IAuditLogger` y triggers de BD).

**P14.4 ¿Existe algún script `seed.sql` o comando `dotnet run --seed`? ¿Qué crea?**
Respuesta: PARCIAL — no hay `seed.sql` ni flag CLI; el seed es **automático en Development**
Evidencia: `src/Cauce.Api/Program.cs:233-236` ·
`src/Cauce.Infrastructure/Persistence/Seeders/SeedingExtensions.cs:20-44`

```csharp
if (app.Environment.IsDevelopment())
{
    await app.Services.RunDevelopmentSeedAsync();
}
```

Detalle: No existe `seed.sql` ni un flag `--seed`. El seeding corre solo, y **únicamente en
Development**. `RunDevelopmentSeedAsync` aplica migraciones pendientes (`context.Database.MigrateAsync`)
y luego ejecuta ocho seeders en orden:

| # | Seeder | Qué crea |
|---|---|---|
| 1 | `UserRolesSeeder` | Catálogo `user_roles` (patient, nutritionist) |
| 2 | `AllergiesSeeder` | Catálogo de alergias |
| 3 | `FoodItemsSeeder` | Catálogo de alimentos (TPCA-CENAN, 928 ítems) |
| 4 | `GlossaryTermsSeeder` | Términos del glosario clínico (~30, en borrador) |
| 5 | `DevAdminSeeder` | Nutricionista demo (`nutricionista.demo@cauce.local`) |
| 6 | `DemoPatientSeeder` | Paciente demo + perfil + historial mínimo (5 comidas, 3 síntomas, 1 IBS-SSS baseline=220, 1 nota) |
| 7 | `RecommendationsModelVersionsSeeder` | `ModelVersion` del ONNX dummy |
| 8 | `MinioBucketSeeder` | Buckets `clinical-reports` y `patient-exports` |

⚠️ **Todo el bloque está envuelto en un `try/catch` que traga cualquier excepción**
(`SeedingExtensions.cs:40-43`): loguea `"Development seeding failed. The application will continue to
start."` y deja arrancar la app. Es el mecanismo por el que el fallo conocido de `MinioBucketSeeder`
(acta A34, `NullReferenceException`) pasa como warning. El efecto colateral: **un fallo de cualquiera
de los ocho seeders deja la base a medio poblar sin que el arranque lo delate.**

---

# Resumen ejecutivo

## Estado de los 8 gaps

| Estado | Cantidad | Gaps |
|---|---|---|
| **ABIERTO** | **7** | 1, 2, 3, 4, 5, 7, 8 |
| **PARCIAL** | **1** | 6 |
| **RESUELTO** | **0** | — |
| **NO APLICA** | **0** | — |

**Ningún gap se resolvió** en las ~5.8 semanas transcurridas. El backend no recibió un solo commit
desde el 10 de julio, así que el estado es idéntico al del snapshot original, con dos matices:

- **GAP 6 es PARCIAL, no ABIERTO.** El flujo de login sí tiene tres tests que lo ejercitan
  (`AuditMiddlewareTests.cs:26, 45, 64`), aunque su sujeto sea la auditoría y no el login. No es "0
  cobertura" literal: es "0 cobertura dedicada, 3 tests colaterales que solo miran el status code".
- **GAP 8 tiene más infraestructura de la esperada.** Las columnas `failed_login_attempts` y
  `locked_until` ya existen en el esquema desde el 24 de junio, los métodos de dominio están escritos y
  probados unitariamente, y el mapeo a 423 ya está en el middleware. Falta exclusivamente el cableado
  en `LoginCommandHandler` y una decisión de diseño (ver Bloqueantes).

## Sorpresas fuera del scope de este prompt

**S1. La sesión de inactividad de Keycloak mata la sesión móvil a los 30 minutos, con o sin endpoint
de refresh.**
`infrastructure/keycloak/import/realm.json:24` fija `ssoSessionIdleTimeout: 1800` (30 min) y el cliente
`cauce-mobile` fija `client.session.idle.timeout: "1800"` (línea 96). El scope `offline_access` —que
elevaría el idle a 30 días vía `offlineSessionIdleTimeout: 2592000`— está declarado como **opcional**
(línea 102), y `KeycloakTokenClient.cs:51` pide **solo `scope=openid`**. Consecuencia: el refresh token
que recibe el móvil está atado a la sesión SSO, que muere tras 30 minutos de inactividad. **Construir
`/auth/refresh` sin resolver esto no cierra US08 CA02:** la sesión seguiría muriendo a la media hora.
Hay que decidir entre subir `ssoSessionIdleTimeout` o pedir `offline_access` en el login del móvil.

**S2. Un endpoint `/auth/refresh` nuevo no quedaría auditado.**
`src/Cauce.Api/Middleware/AuditingMiddleware.cs:46-47` clasifica el evento por sufijo literal de ruta
(`path.EndsWith("/auth/login")` y `"/auth/logout"`). Una ruta `/auth/refresh` no cae en ninguna rama y
**no generaría fila en `audit_logs`**. Dado que la decisión de usar passthrough en vez de PKCE se tomó
precisamente para no perder trazabilidad de accesos (Ley N° 29733), conviene decidir si la renovación
debe auditarse y, si sí, extender el middleware en el mismo cambio.

**S3. El acta M5 del mobile sigue vigente en git y contradice la arquitectura acordada.**
`mobile/DECISIONS-BLOCK-MOBILE-1.md` declara "Acta M5: Auth con `flutter_appauth` y PKCE directo a
Keycloak". La `MATRIZ-IDENTIDAD.md` la da por anulada, pero esa matriz **no está commiteada** y **no
existe acta M9** que formalice la anulación. La única fuente de verdad versionada del mobile dice hoy
lo contrario de lo que se va a construir. Además `flutter_appauth: ^8.0.1` sigue en `pubspec.yaml`
existiendo solo por M5.

**S4. Tres documentos críticos existen en disco pero no están en git.**
`backend/docs/api/CONTRACT-IDENTITY-v1.md`, `backend/CLAUDE.md` (v2.0.2) y
`docs/traceability/MATRIZ-IDENTIDAD.md`, más las ocho actas A30–A37 del repo `docs`. Todo el trabajo
analítico del 8 al 13 de julio está sin commitear y depende de que no se limpien los working trees.
Las actas A35 y A36 son justamente las que documentan los fixes de Keycloak que faltan en `realm.json`.

**S5. El backend no tiene CI.**
No existe `.github/workflows/` en el repo `backend`. El único workflow del proyecto es el de Flutter
(`mobile/.github/workflows/ci.yml`). Las 625 pruebas que declara `CLAUDE.md` solo corren cuando alguien
las lanza a mano, y los 24 tests de integración se omiten en silencio si Docker no está.

**S6. `src/Cauce.Api/Cauce.Api.http` es scaffold muerto.**
Apunta a `http://localhost:5008/weatherforecast/`. Ni el puerto ni el endpoint existen en este
proyecto. Es ruido que puede confundir a quien busque un consumidor de referencia.

**S7. El mobile tiene desactualización de dependencias con saltos de major.**
`flutter_appauth` va 4 majors atrás, `go_router` 3, `flutter_secure_storage` 2, `riverpod_annotation`
2. `sqlite3_flutter_libs` aparece marcado `0.6.0+eol`. Conviene decidir el upgrade **antes** de escribir
código sobre esas APIs, no después.

**S8. `generate: true` sin `l10n.yaml` ni archivos `.arb`.**
`mobile/pubspec.yaml:64` activa la generación de localizaciones, pero no hay `l10n.yaml` ni ningún
`.arb`. El acta M8 (arb + `es_PE`) está declarada y no implementada.

**S9. El seeding de Development se traga todos los errores.**
`SeedingExtensions.cs:40-43` envuelve los ocho seeders en un `try/catch` único que solo loguea. Un
fallo en el seeder 3 deja los seeders 4 a 8 sin correr y la app arranca igual, sin señal visible.

## Bloqueantes descubiertos para escribir el prompt de fixes

Son decisiones que **no se pueden tomar leyendo código**. Cada una cambia materialmente el alcance del
fix.

**B1. GAP 8 — ¿Quién es la fuente de verdad del lockout: Keycloak o la tabla `users`?**
Hoy hay dos mecanismos a medio hacer. Keycloak tiene `bruteForceProtected: true` con `failureFactor: 5`
y **funciona** (bloquea de verdad), pero responde 401 genérico y `KeycloakTokenClient.cs:61-65` colapsa
400 y 401 en `InvalidCredentialsException`. La tabla `users` tiene las columnas y el dominio tiene los
métodos, pero **nadie los invoca**. Las tres salidas posibles tienen costos muy distintos:
(a) lockout local — cablear `RegisterFailedLogin`/`IsLocked` en el handler, riesgo de desincronía con
el contador de Keycloak; (b) leer el estado de Keycloak vía Admin API (`FindByEmailAsync` ya existe)
para traducir el 401 en 423; (c) borrar el código muerto y aceptar que US05 CA02 no se cumple.
**Sin esta decisión el fix no se puede especificar.**

**B2. GAP 2 — ¿A dónde debe apuntar el link de recuperación?**
El cambio de código es trivial (una clave de configuración), pero el destino no está decidido: deep
link `cauce://` para el móvil, página del portal web React, o una página HTML servida por el backend
(que hoy no puede servir ninguna, P2.5). Además, si el mismo backend atiende a móvil y portal, un solo
`AppBaseUrl` no alcanza para ambos destinos: haría falta parametrizar por cliente.

**B3. GAP 1 — La renovación no se cierra solo con el endpoint (ver S1 y S2).**
Hay que decidir en el mismo movimiento: (i) si el móvil pide `offline_access` o se sube
`ssoSessionIdleTimeout`, porque si no la sesión muere a los 30 min igual; (ii) si `/auth/refresh` debe
auditarse, lo que implica tocar `AuditingMiddleware`; (iii) qué política de rate limit le corresponde
(hoy `auth-login` es 10/min·IP, que para un refresh automático cada 15 min por dispositivo podría
quedar corto o largo según el patrón de uso).

**B4. GAP 7 — No se puede exportar el realm con el entorno caído.**
Los fixes A35 y A36 viven **solo en la consola de una instancia de Keycloak que hoy no está
corriendo** (P0.13: puerto 8081 cerrado, Docker sin responder). Hay dos caminos y ninguno es gratis:
(a) levantar el stack, confirmar que el volumen de Keycloak conserva la configuración manual, y
exportar; (b) si el volumen se perdió, **escribir el `clientScopes` y los `protocolMappers` a mano** en
`realm.json` a partir de las actas. **Antes de escribir el prompt de fixes hay que verificar si el
volumen de Keycloak sobrevivió.** Si no sobrevivió, el alcance del GAP 7 crece.

**B5. GAP 5 — Falta definir el shape y el borde de usuario ausente.**
`isInActivePilot` no está en ningún claim ni en ningún endpoint, así que solo puede venir de la fila
local. Pero `LoginCommandHandler.cs:43` contempla que `user` sea `null` (Keycloak autentica y no hay
fila local) y hoy eso pasa en silencio. Hay que decidir el contrato: ¿qué campos exactos, y qué hace el
endpoint cuando no hay usuario local — 200 con campos nulos, o error?

**B6. Falta un acta M9 que anule formalmente M5 (ver S3).**
El prompt de fixes de backend y el de Mobile-1b se apoyan en que el móvil usa `POST /auth/login`. La
única fuente versionada del mobile dice PKCE directo. Conviene commitear la anulación antes de que
Mobile-1b se escriba sobre una premisa que el repo contradice.

**B7. No hay red de seguridad para verificar los fixes de contrato.**
Cero tests sobre el envelope de error (P3.4) y cero tests dedicados de login/logout (P6.1). Fixear los
gaps 3 y 5 no rompe nada, pero tampoco hay forma de comprobar que quedaron bien. **El prompt de fixes
debería exigir tests como parte de cada fix**, no dejarlos para el GAP 6 al final.

**B8. El entorno de verificación está caído.**
Docker Desktop tiene procesos vivos pero el engine no responde al CLI. Sin él no corren los 24 tests de
integración, no se puede exportar el realm, no se puede validar ningún fix end-to-end y las preguntas
de Fase 5 quedan sin respuesta. **Restaurar Docker es prerequisito operativo de cualquier trabajo de
fixes.**
