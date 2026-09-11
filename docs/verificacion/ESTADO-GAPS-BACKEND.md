# Estado de los Gaps de Backend — Pre-Mobile-1b

> **Este documento cubre tres bloques.** La primera parte, histórica, es el diagnóstico y cierre de los 8
> gaps previos a Mobile-1b. Los bloques **Backend-Fix-2** y **Nutritionist-Activation-1** tienen su propia
> sección, más abajo, y no alteran nada de lo anterior. Ir a [Backend-Fix-2](#backend-fix-2) o a
> [Nutritionist-Activation-1](#nutritionist-activation-1).

**Fecha de verificación:** 20 de agosto de 2026
**Fecha de cierre:** 25 de agosto de 2026
**Backend al verificar:** `develop` @ `b2dfd9c` (tag `v0.6.2-dev-seed`)
**Backend al cerrar:** `feature/backend-fixes-pre-mobile-1b`
**Mobile:** `develop` @ `f522073`
**Reporte completo:** [`REPORTE-VERIFICACION-03.md`](REPORTE-VERIFICACION-03.md)

**Leyenda de estados:** ABIERTO · PARCIAL · RESUELTO · NO APLICA
**Leyenda de estimados:** S (< 1 h) · M (1–4 h) · L (4–8 h) · XL (> 8 h)

---

## Estado de cierre

| Gap # | Nombre | Estado | Commit | Fase |
|---|---|---|---|---|
| 1 | Refresh token endpoint | **RESUELTO** | `56d8f93` | 6 |
| 2 | Password reset link | **RESUELTO** | `b054719` | 4 |
| 3 | Validation error keys PascalCase | **RESUELTO** | `0eb63fd` | 1 |
| 4 | Consent text/hash endpoint | **RESUELTO** | `f8a7633` | 2 |
| 5 | Login no retorna user data | **RESUELTO** | `d6466b2` | 5 |
| 6 | Login sin cobertura de tests | **RESUELTO** | `1493b9f` | 8 |
| 7 | Keycloak realm.json desactualizado | **RESUELTO** ⚠️ | `7782072` (repo `infrastructure`) | 3 |
| 8 | AccountLockedException huérfana | **RESUELTO** | `b20e196` | 7 |

**Recuento final: 8 RESUELTOS, 0 ABIERTOS, 0 PARCIALES.**

⚠️ **El gap 7 tiene una condición pendiente.** El `realm.json` se validó importándolo en un contenedor
Keycloak 25 efímero con base H2, con tres checks explícitos (los 11 scopes built-in se crean solos,
`basic` se resuelve por nombre en ambos clientes, y un token real trae `aud=cauce-backend` y `sub`).
Falta **una ventana de reset controlado del stack real** —bajar Keycloak, subir con el realm nuevo,
smoke con el paciente demo— antes de darlo por cerrado sin reservas: H2 y Postgres tienen ~99 % de
paridad en imports de realm, no 100 %.

### Qué destapó la validación del gap 7

El enfoque que planteaba el prompt habría sido **peor que no hacer nada**. Declarar la clave
`clientScopes` en un realm import **no fusiona con los client scopes built-in: los reemplaza**. Un
`realm.json` que declaraba solo `cauce-backend-audience` importó con `Realm 'cauce' imported` y cero
errores en los logs, y produjo un realm con **2 client scopes en vez de 12**, sin `basic`, sin
`profile` y sin `email` — reintroduciendo el defecto del acta A36 y agravándolo.

Por eso el fix final **no declara `clientScopes`**: el `oidc-audience-mapper` se persiste dentro del
bloque `protocolMappers` de cada cliente. Es una divergencia deliberada respecto del acta A35, que lo
documentaba como scope compartido porque así se aplicó en consola. **El acta A35 necesita una "Nota de
persistencia"** que explique esta divergencia; queda como tarea de documentación.

---

## Tabla ejecutiva original (estado al 20 de agosto)

Se conserva como registro histórico del diagnóstico.

| Gap # | Nombre | Estado | Evidencia principal | Impacto en Mobile-1b | Estimado fix |
|---|---|---|---|---|---|
| **1** | Refresh token endpoint | **ABIERTO** | `AuthController.cs:19-169` declara 5 acciones, ninguna de refresh · `IKeycloakTokenClient.cs:8-29` solo tiene `LoginAsync` y `LogoutAsync` · `grant_type=refresh_token` no aparece en `src/` | **Bloqueante.** El access token dura 900 s. Sin refresh, la sesión muere a los 15 min y US08 CA02 no se puede cumplir. Ver S1: además hay que resolver el idle de 30 min del SSO | **M** |
| **2** | Password reset link | **ABIERTO** | `ClientUrlProvider.cs:26-30` arma `{AppBaseUrl}/auth/password-reset?token=…` · `appsettings.Development.json` fija `AppBaseUrl = "http://localhost:5074"` (el backend) · `Program.cs:228-229` no expone ninguna ruta fuera de `api/v{version}` | **Bloqueante para US07 end-to-end.** El correo llega, pero el link da 404. El paciente nunca completa el reset desde la app | **S** (código) · decisión pendiente |
| **3** | Validation error keys PascalCase | **ABIERTO** | `ExceptionHandlingMiddleware.cs:271-288` agrupa por `failure.PropertyName` (PascalCase) · `Program.cs:127-133` fija `PropertyNamingPolicy` pero **no** `DictionaryKeyPolicy` · `ConfigureHttpJsonOptions` nunca se llama | **Medio.** El móvil puede mapear PascalCase, pero es inconsistente con el resto del contrato (todo camelCase) y frágil. Afecta US01 CA02 (error por campo) | **S** |
| **4** | Consent text/hash endpoint | **ABIERTO** | No existe `ConsentController` · `IConsentService` expone `GetCurrentText()`/`GetCurrentVersion()`/`GetCurrentTextHash()` pero ningún controller los publica · único endpoint de consent: `PatientsController.cs:169` (`me/consent/pdf`, exige `Policy=Patient`) | **Bloqueante para US01.** Sin el texto, el móvil no puede computar un `consentTextHash` válido → todo registro falla con 400 `consent_text_mismatch`. Replicar el texto en el cliente es frágil: el hash no normaliza nada (sin `Trim`, sin CRLF→LF) | **S** |
| **5** | Login no retorna user data | **ABIERTO** | `LoginCommand.cs:23-28` — `LoginResult` solo trae `AccessToken`, `RefreshToken`, `ExpiresIn`, `RefreshExpiresIn`, `TokenType` · no existe `GET /users/me` (`UsersController.cs:37` solo tiene `PUT me/fcm-token`) | **Medio.** Mitigable decodificando el JWT (`sub`, `realm_access.roles`, `email`), salvo **`isInActivePilot`**, que no está en ningún claim ni endpoint. El handler ya carga el `User` (`LoginCommandHandler.cs:42`), así que no cuesta query extra | **M** |
| **6** | Login sin cobertura de tests | **PARCIAL** | No existe `LoginCommandHandlerTests.cs` ni `AuthControllerTests.cs` · 3 tests colaterales en `AuditMiddlewareTests.cs:26, 45, 64` solo assertean status code y filas de `audit_logs` · Keycloak siempre mockeado (`FakeKeycloakTokenClient.cs`) | **Indirecto.** No bloquea construir el cliente, pero deja los fixes 1, 3, 5 y 8 sin red de seguridad. Ninguna condición real de Keycloak (lockout, `VERIFY_EMAIL`, audience, `sub`) se ejercita jamás | **M** |
| **7** | Keycloak realm.json desactualizado | **ABIERTO** | `git log` del archivo: **un solo commit, 2026-06-23** (`b5079ee`) · grep de `oidc-audience-mapper` → **0 coincidencias** · `realm.json:101` — `defaultClientScopes` sin `basic` ni `cauce-backend-audience` · actas A35 y A36 dicen explícitamente que el fix es de consola | **Bloqueante latente.** Un `docker compose down -v` + re-import emite tokens sin `aud=cauce-backend` (**401**) y sin `sub` (**403**), sin señal de causa. No hay script ni Makefile de re-export (P7.7) | **M** |
| **8** | AccountLockedException huérfana | **ABIERTO** | `AccountLockedException.cs:9-25` definida con `LockedUntil` · **0 sitios de `throw new AccountLockedException` en `src/`** · `User.RegisterFailedLogin()` (`User.cs:218`) con **0 callers en `src/`** · `LoginCommandHandler.cs:36-56` no consulta lockout · mapeo muerto en `ExceptionHandlingMiddleware.cs:163-164` | **Medio.** El bloqueo real **sí ocurre** (Keycloak, `failureFactor: 5`), pero llega como 401 `invalid_credentials`, indistinguible de contraseña incorrecta. US05 CA02 exige mostrar el tiempo de espera y hoy es imposible | **L** |

---

## Recuento al 20 de agosto (diagnóstico)

| Estado | Cantidad | Gaps |
|---|---|---|
| ABIERTO | **7** | 1, 2, 3, 4, 5, 7, 8 |
| PARCIAL | **1** | 6 |
| RESUELTO | **0** | — |
| NO APLICA | **0** | — |

**Esfuerzo estimado entonces:** 3 × S + 3 × M + 1 × L + 1 × M (tests) ≈ **14 a 25 horas**, sin contar
las decisiones de arquitectura pendientes (B1–B6 del reporte) ni la restauración del entorno Docker
(B8).

---

## Notas sobre los estimados

**GAP 1 — M.** Requiere: `RefreshAsync` en `IKeycloakTokenClient` + implementación (la URL del token
endpoint ya está resuelta en `KeycloakTokenClient.cs:38`), `RefreshTokenCommand`/`Handler`/`Validator`,
DTO de contrato, acción en `AuthController` con `[ProducesResponseType]`, política de rate limit y
tests. Sube a **L** si además hay que resolver el idle de sesión (S1) y la auditoría del refresh (S2).

**GAP 2 — S de código, pero la decisión domina.** El cambio es una clave de configuración
(`Email:AppBaseUrl`). Lo que no está decidido es el destino (deep link `cauce://` vs portal web vs
página servida por el backend) y si un solo valor alcanza para móvil y portal simultáneamente. Si hay
que parametrizar por cliente, sube a **M**.

**GAP 3 — S.** Dos rutas posibles: transformar las claves en `BuildValidationProblem`
(`ExceptionHandlingMiddleware.cs:273-277`), o añadir `ConfigureHttpJsonOptions` con
`DictionaryKeyPolicy = JsonNamingPolicy.CamelCase`. La segunda es más global y afecta a cualquier
diccionario serializado por el middleware — conviene revisar el impacto antes de elegirla. **Cero tests
romperían** (P3.3), pero por lo mismo el fix debe traer su propio test.

**GAP 4 — S.** Toda la lógica ya existe en `ConsentService`. Falta un controller anónimo que exponga
`{ version, text, hash }`. Lo único a decidir es la ruta y si se publica el hash o se deja que el
cliente lo compute (publicarlo elimina de raíz el riesgo de divergencia byte a byte).

**GAP 5 — M.** El handler ya tiene la entidad `User` en memoria. El trabajo real es acordar el shape y
resolver el borde de `user == null` (`LoginCommandHandler.cs:43`), que hoy pasa en silencio. **Cero
tests romperían** (P13.2).

**GAP 6 — M.** `LoginCommandHandlerTests` + `LogoutCommandHandlerTests` siguiendo el patrón de
`RegisterPatientCommandHandlerTests.cs`, más tests de integración del envelope de error. No cubre las
condiciones reales de Keycloak: eso exigiría un contenedor de Keycloak en Testcontainers, que hoy no
existe y sería un esfuerzo aparte (**L** adicional).

**GAP 7 — M, con riesgo de crecer.** Si el volumen de Keycloak conserva la configuración de consola,
es exportar y commitear. Si se perdió, hay que reconstruir a mano la sección `clientScopes` con el
`oidc-audience-mapper` y reasignar los `defaultClientScopes` de `cauce-mobile` y `cauce-web-portal` a
partir de las actas A35 y A36. **Verificar el estado del volumen es prerequisito para estimar.**
Conviene aprovechar y añadir el script de re-export que hoy no existe.

**GAP 8 — L.** Es el único gap donde el costo no está en el código sino en la decisión (B1): dos
mecanismos de lockout a medio hacer, uno funcional pero mudo (Keycloak) y otro completo pero
desconectado (dominio local). Cualquiera de los tres caminos —lockout local, consulta al Admin API, o
borrar el código muerto— exige además declarar el 423 en `[ProducesResponseType]` y regenerar el
OpenAPI, donde hoy no aparece en ningún endpoint (P8.7).

---

## Dependencias entre gaps y decisiones

| Gap | Bloqueado por | Naturaleza del bloqueo |
|---|---|---|
| 1 | B3 | Decidir `offline_access` vs subir `ssoSessionIdleTimeout`; y si el refresh se audita |
| 2 | B2 | Decidir el destino del link (deep link, portal, o página del backend) |
| 3 | — | Ninguno. Listo para implementar |
| 4 | — | Ninguno. Solo elegir ruta y si se publica el hash |
| 5 | B5 | Definir shape exacto y comportamiento con usuario local ausente |
| 6 | B7 | Conviene distribuirlo dentro de cada fix, no dejarlo al final |
| 7 | B4, B8 | Exige Docker arriba y saber si el volumen de Keycloak sobrevivió |
| 8 | B1 | Decidir la fuente de verdad del lockout |

**Gaps listos para especificar hoy sin más decisiones: 3 y 4.**
**Gaps que exigen levantar el entorno antes de estimar: 7.**
**Gaps que exigen una decisión de arquitectura previa: 1, 2, 5, 8.**

---

## Backend-Fix-2

**Última actualización:** 2026-09-08
**Backend al cerrar:** `feature/backend-fix-2-for-mobile`, tag `v0.8.0-backend-fix-2`
**Alcance:** las tres deudas técnicas de identidad que quedaron abiertas tras el bloque anterior, más la
documentación formal de las deudas que se difieren.

Los identificadores de commit no se replican aquí ni en las actas: viven en el historial de git, que es
su fuente de verdad. Cada acta declara en su estado el bloque y el tag donde se resolvió.

### Deudas cerradas en Backend-Fix-2

| Acta | Deuda | Archivo | Cómo se resolvió |
|---|---|---|---|
| **A39** | `emailVerified` desincronizado con Keycloak | [`A39-lazy-sync-emailverified.md`](../decisions/A39-lazy-sync-emailverified.md) | El login consulta el estado real en Keycloak y promueve el valor local si difiere. Es unidireccional (solo `false → true`) y tolerante a fallos: si la Admin API no responde, la sesión continúa con el valor local |
| **A40** | Sin reenvío del correo de verificación | [`A40-verification-email-resend-endpoint.md`](../decisions/A40-verification-email-resend-endpoint.md) | Endpoint anónimo `POST /auth/verification-email/resend`, con respuesta 200 uniforme que no revela si la cuenta existe ni si está verificada, y rate limit de 3/hora particionado por correo normalizado |
| **A41** | Sin canje de código de invitación posterior al registro | [`A41-nutritionist-assignment-endpoint.md`](../decisions/A41-nutritionist-assignment-endpoint.md) | Endpoint `POST /patients/me/nutritionist-assignment` con `Policy=Patient`, precedido del refactor que extrajo la vinculación a un servicio dedicado. El código no se consume si el canje falla |

### Deudas diferidas identificadas

| Acta | Deuda | Archivo | Se resuelve en |
|---|---|---|---|
| **A38** | `isInActivePilot` hardcodeado | [`A38-isinactivepilot-hardcoded.md`](../decisions/A38-isinactivepilot-hardcoded.md) | **Sin bloque asignado.** Bloqueada por una dependencia externa: la lista definitiva de pacientes del piloto, que debe entregar el Complejo Hospitalario Guillermo Kaelín |
| **A47** | Sin ciclo de vida de cuentas de nutricionista | [`A47-nutritionist-deferred-activation-debt.md`](../decisions/A47-nutritionist-deferred-activation-debt.md) | **Nutritionist-Activation-1.** Sin bloqueante externo: `Activate`, `Suspend` y `Reactivate` existen en el dominio pero no tienen ningún llamador en la aplicación |
| **A48** | Asimetría en la persistencia de la auditoría de intentos anónimos | [`A48-audit-persistence-asymmetry.md`](../decisions/A48-audit-persistence-asymmetry.md) | **Por definir, recomendado antes de Deployment-1.** `password-reset/request` descarta su fila de auditoría cuando la cuenta no existe; `verification-email/resend` la persiste. Descubierta en el smoke de cierre |

### Decisiones emergentes durante la ejecución

Cuatro decisiones de arquitectura surgieron durante el bloque y se registraron antes de aplicarse, según
la regla R8.

| Acta | Tema |
|---|---|
| [**A43**](../decisions/A43-domain-event-for-nutritionist-notification.md) | Reemplaza a la decisión D6: la notificación al nutricionista se resuelve reusando el evento de dominio existente, con el texto parametrizado por contexto. Evitó un doble correo por canje |
| [**A44**](../decisions/A44-rate-limit-partition-by-request-body.md) | Partición del rate limit por el correo del cuerpo de la petición, mediante un middleware previo al limitador |
| [**A45**](../decisions/A45-test-migration-on-constructor-refactor.md) | Marco para migrar pruebas en un refactor por inyección de constructor, distinguiendo el intercambio mecánico de la reexpresión al nivel correcto |
| [**A46**](../decisions/A46-swashbuckle-cli-tool.md) | Swashbuckle CLI como herramienta local para regenerar el snapshot OpenAPI de forma reproducible |

### Hallazgo de instrumentación: `Cauce.Api` no se mide

Detectado al medir la cobertura de cierre del bloque. **No requiere acta formal: es un cambio de
configuración, no una decisión de arquitectura.** Se registra aquí para que un bloque futuro lo tome.

`Cauce.Api` **no aparece en ninguno de los cuatro archivos `coverage.cobertura.xml`** que produce
`dotnet test --collect:"XPlat Code Coverage"`. Solo se instrumentan `Cauce.Domain`, `Cauce.Application`
y `Cauce.Infrastructure`. Los controllers, el middleware y las ocho políticas de rate limit no tienen
cifra de cobertura, pese a que las 186 pruebas de integración los ejercitan vía
`WebApplicationFactory`.

La causa es que las pruebas de integración referencian el proyecto de API y coverlet instrumenta los
ensamblados que la corrida carga, pero el filtro efectivo deja fuera a `Cauce.Api`. El arreglo es
declarar el include explícitamente, con un `runsettings` o con
`/p:Include="[Cauce.*]*"`, y verificar que el archivo resultante trae el paquete.

**Segundo hallazgo, del mismo origen.** La cifra histórica de «>85% global» que declaraban `CLAUDE.md`
y los criterios de calidad del bloque **incluye las migraciones de EF Core**, que son código generado.
`Cauce.Infrastructure` tiene 25 684 líneas instrumentadas, de las cuales **20 065 son migraciones**,
que se ejecutan al crear la base en las pruebas y cuentan como cubiertas. Medido el 2026-09-09:

| Medición | Líneas | Ramas |
|---|---|---|
| Incluyendo migraciones EF | 89,6 % | 57,1 % |
| **Excluyendo migraciones EF** | **76,1 %** | **57,1 %** |

La cifra defendible es la segunda. Un bloque futuro debería excluir las migraciones del cálculo, además
de instrumentar `Cauce.Api`, para que el número que se reporte signifique algo.

> **Corrección del 2026-09-11.** Las dos cifras de esta tabla salieron de una fusión de los reportes que
> contaba dos veces cada archivo de `Cauce.Domain`. Remedido este mismo tag con la fusión por ruta
> absoluta, las cifras correctas son **79,8 %** de líneas sin migraciones y 91,8 % con migraciones, con
> 60,1 % de ramas en las dos filas. Detalle en [Nutritionist-Activation-1](#nutritionist-activation-1).

---

## Nutritionist-Activation-1

**Última actualización:** 2026-09-11
**Backend al cerrar:** `feature/nutritionist-activation-1`
**Alcance:** activación de las cuentas de nutricionista (acta A47), cierre del canje sin validar en el
registro, y respuesta del endpoint admin de provisión (acta A49).

### Actas del bloque

| Acta | Tema | Estado |
| --- | --- | --- |
| [**A49**](../decisions/A49-admin-nutritionist-provisioning-response.md) | Respuesta de la provisión: `status`, `Location`, `activationEmailSent` y 502 declarado | Resuelta |
| [**A51**](../decisions/A51-nutritionist-activation-mechanism.md) | Activación en la primera autenticación, por login o por el behavior del pipeline | Resuelta |
| [**A52**](../decisions/A52-keycloak-activation-link-and-resend.md) | Enlace de Keycloak para definir la contraseña y su reenvío. Resuelve de paso la cuenta huérfana | Resuelta |
| [**A53**](../decisions/A53-nutritionist-status-in-invitation-codes.md) | Estado del nutricionista al registrarse con un código y al generarlo | Resuelta |
| [**A54**](../decisions/A54-nutritionist-operating-terms-deferred.md) | Términos operativos del nutricionista | Diferida |
| [**A55**](../decisions/A55-suspension-session-enforcement-deferred.md) | La suspensión no corta sesiones ni tokens emitidos | Diferida |
| [**A56**](../decisions/A56-invitation-revocation-on-suspension-deferred.md) | Los códigos de invitación no se revocan al suspender | Diferida |

A47 queda resuelta en su parte de activación. A50 sigue reservada: el hallazgo menor del smoke de
Backend-Fix-2 no tiene archivo propio.

### Verificación

- **Suite completa en secuencial:** 840 verdes, 0 omitidas (344 Domain, 203 Application, 85
  Infrastructure, 208 integración).
- **Smoke de punta a punta** contra el stack real (Keycloak 25.0.6, Mailpit, Postgres), sin pasos por SQL:

  | Escenario | Resultado |
  | --- | --- |
  | Provisión | 201 `PendingActivation`, `Location` resuelve al `GET`, y ningún correo de credenciales del backend |
  | Enlace de Keycloak | Llega "Actualiza tu cuenta"; tras definir la contraseña, la cuenta sigue pendiente hasta autenticarse |
  | Token directo de Keycloak y `POST /invitations` | 201, la cuenta pasa a `Active` en la misma petición, auditada con `trigger: authenticated_request` |
  | `POST /auth/login` | 200, la cuenta pasa a `Active`, auditada con `trigger: login` |
  | Reenvío | 204 con segundo correo y auditoría a una cuenta pendiente; 409 `nutritionist_not_pending_activation` a una activa |
  | Registro de paciente con el código | 201 y el código queda usado; reusarlo da 400 `invitation_code_already_used` |

  Los usuarios del smoke (prefijo `smoke-na1-`) quedan en el entorno de desarrollo, en Keycloak y en la
  base. Sus filas no se pueden borrar porque la bitácora inmutable las referencia, y dejar solo el lado de
  Keycloak crearía cuentas locales sin identidad.

- **Cobertura del código nuevo:**

  | Pieza | Líneas |
  | --- | --- |
  | Behavior de activación, reenvío y `GET` admin | 100 % |
  | Servicio de activación | 97,8 %; la única línea no cubierta es la rama por defecto de un `switch` exhaustivo |
  | Excepciones `NutritionistNotAvailableException`, `NutritionistNotPendingActivationException` y `NutritionistNotFoundException` | 100 % |
  | Métodos modificados (login, validación del código en el registro, generación, `SendUpdatePasswordEmailAsync`) | 100 % |
  | `AdminController` / `InvitationsController` | 100 % / 84,6 %; la rama no cubierta es preexistente |

### Corrección al hallazgo de instrumentación

En la medición de este bloque, **`Cauce.Api` sí aparece instrumentado**: 82,2 % de líneas y 72,0 % de
ramas. La ausencia de la sección anterior tampoco dependía del código: con el mismo comando, el tag
`v0.8.0-backend-fix-2` también lo instrumenta (81,7 % de líneas). Vino de cómo se hizo aquella corrida,
que no quedó registrada.

| Medición (2026-09-11) | Líneas | Ramas |
| --- | --- | --- |
| Global sin migraciones ni `Cauce.Api` | **80,5 %** | 60,9 % |
| Global sin migraciones, con `Cauce.Api` | 80,7 % | 62,7 % |
| Global con migraciones | 91,5 % | 62,7 % |
| Sin código generado ni `Cauce.Api` | 81,6 % | 62,8 % |

La cifra principal sigue el criterio del bloque anterior: excluye las migraciones de EF Core y deja fuera
`Cauce.Api`. La solución tiene otro código generado que ese criterio no excluye, el `RegexGenerator.g.cs`
que el generador de expresiones regulares produce para `Cauce.Infrastructure`: 664 líneas, 59,6 %
cubiertas. Sin él la cifra sube a 81,6 %, la de la última fila.

El cálculo fusiona los cuatro `coverage.cobertura.xml` usando como clave la ruta absoluta del archivo y el
número de línea. Una línea cuenta como cubierta si alguna de las cuatro corridas la ejecutó; para las
ramas se toma, por línea, el mayor número de condiciones cubiertas.

La ruta absoluta es necesaria porque coverlet registra cada archivo con un nombre relativo a la raíz de
fuentes de su propio reporte, y esa raíz no es la misma en los cuatro: `Cauce.Domain.Tests` usa
`src\Cauce.Domain\` y los otros tres usan `src\`. Fusionar por el nombre relativo parte en dos cada
archivo de `Cauce.Domain`, que queda contado una vez cubierto y otra sin cubrir. Con ese defecto, esta
misma corrida daba 76,8 % en lugar de 80,5 %.

**El 76,1 % de Backend-Fix-2 tenía el mismo defecto.** Se remidió el tag `v0.8.0-backend-fix-2` con el
mismo comando, con 776 pruebas verdes y 0 omitidas, y se fusionaron sus reportes de las dos maneras:

| Tag `v0.8.0-backend-fix-2` | Fusión por nombre relativo | Fusión por ruta absoluta |
| --- | --- | --- |
| Sin migraciones ni `Cauce.Api` | 76,1 % | **79,8 %** |
| Con migraciones, sin `Cauce.Api` | 89,6 % | 91,8 % |

La fusión por nombre relativo reproduce exactamente las dos cifras publicadas en aquel bloque, así que fue
el método de aquella medición. Con el mismo criterio, este bloque pasa de 79,8 % a 80,5 % de líneas y de
60,1 % a 60,9 % de ramas; sin ningún código generado, de 81,0 % a 81,6 %.
