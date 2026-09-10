# Backend-Fix-2 — cierre de las deudas de identidad pendientes para el móvil

Cierra las tres deudas técnicas de identidad que quedaron abiertas tras el bloque de los 8 gaps
pre-Mobile-1b, documenta las que se difieren, y deja el contrato de identidad y el snapshot OpenAPI
sincronizados con el código.

**Rama:** `feature/backend-fix-2-for-mobile` → `develop`
**Tag previsto sobre el commit de merge:** `v0.8.0-backend-fix-2`

---

## Qué cierra

| Acta | Deuda | Resolución |
| --- | --- | --- |
| **A39** | `emailVerified` desincronizado con Keycloak | El login consulta el estado real en Keycloak y promueve el valor local si difiere, en la misma transacción que la marca de último acceso. Unidireccional (solo `false → true`) y tolerante a fallos: si la Admin API no responde, la sesión continúa con el valor local |
| **A40** | Sin reenvío del correo de verificación | Endpoint anónimo `POST /auth/verification-email/resend`, con respuesta 200 uniforme que no revela si la cuenta existe ni si está verificada, y rate limit de 3/hora **particionado por correo normalizado**, no por IP |
| **A41** | Sin canje de código de invitación posterior al registro | Endpoint `POST /patients/me/nutritionist-assignment` con `Policy=Patient`, precedido del refactor que extrajo la vinculación a `IPatientNutritionistAssignmentService`. **El código no se consume si el canje falla** |

## Decisiones emergentes registradas durante la ejecución

Cuatro decisiones de arquitectura surgieron durante el bloque y se documentaron antes de aplicarse.

| Acta | Tema |
| --- | --- |
| **A43** | Reemplaza a la decisión D6 del prompt. La notificación al nutricionista reusa el evento de dominio existente con el texto parametrizado por contexto. **Evitó un doble correo por canje**: la premisa del prompt, que el handler de registro no notificaba, era falsa |
| **A44** | Partición del rate limit por el correo del cuerpo de la petición, mediante un middleware previo al limitador. Es la primera política que particiona por contenido del cuerpo |
| **A45** | Marco para migrar pruebas en un refactor por inyección de constructor, distinguiendo el intercambio mecánico de la reexpresión al nivel correcto |
| **A46** | Swashbuckle CLI como herramienta local para regenerar el snapshot OpenAPI de forma reproducible |

## Contrato

- `docs/api/CONTRACT-IDENTITY-v1.md` → **v1.2**, con §2.10 (reenvío) y §2.11 (canje).
- `docs/api/openapi-v1.1.0.json` → **53 paths, 61 operaciones** (antes 51 / 57).
- `errorCode` nuevos: `patient_already_assigned` y `nutritionist_not_available`, este último con la
  extensión `reason` (`pending_activation` | `inactive` | `suspended`).

> **Acción pendiente para el móvil:** su snapshot local sigue en `openapi-v1.0.0.json`. Debe regenerar
> su cliente contra `openapi-v1.1.0.json` en Mobile-1.5 o Mobile-2.

---

## Métricas

### Suite

**776 pasados · 0 fallidos · 0 omitidos.** Cero regresiones.

Ejecutada en secuencial, que es obligatorio: `dotnet test -- xUnit.ParallelizeTestCollections=false`.
En paralelo, Testcontainers satura Docker y omite las colecciones de integración.

| Proyecto | Línea base | Ahora | Δ |
| --- | --- | --- | --- |
| Cauce.Domain.Tests | 335 | 335 | — |
| Cauce.Application.Tests | 105 | 173 | +68 |
| Cauce.Infrastructure.Tests | 73 | 82 | +9 |
| Cauce.Api.IntegrationTests | 165 | 186 | +21 |
| **Total** | **678** | **776** | **+98** |

Por fase: +17 (F1), +30 (F2), +12 (F3), +39 (F4). F5 y F6 fueron documentales.

### Cobertura

**Del código construido en este bloque**, que es lo que el PR agrega:

| Componente | Líneas | Cobertura |
| --- | --- | --- |
| `LoginCommandHandler` | 97/97 | 100 % |
| `ResendVerificationEmailCommandHandler` | 34/34 | 100 % |
| `ResendVerificationEmailCommandValidator` | 12/12 | 100 % |
| `AssignNutritionistCommandValidator` | 9/9 | 100 % |
| `PatientNutritionistAssignmentService` | 41/41 | 100 % |
| `PatientLinkedToNutritionistEventHandler` | 30/30 | 100 % |
| `KeycloakAdminClient.GetUserEmailVerifiedAsync` | 11/11 | 100 % |
| `KeycloakAdminClient.SendVerifyEmailAsync` | 7/7 | 100 % |
| `AssignNutritionistCommandHandler` | 101/107 | 94,4 % |

Las 6 líneas sin cubrir del handler de canje son dos guardas defensivas de inconsistencia de datos: el
`LogCritical` del nutricionista inexistente y el `if (!assigned)` que no debería ocurrir.

**Cobertura global, por capa:**

| Capa | Líneas | Ramas |
| --- | --- | --- |
| Cauce.Application | 78,8 % | 58,1 % |
| Cauce.Domain | 68,2 % | 55,9 % |
| Cauce.Infrastructure | 79,2 % | 57,3 % |
| Cauce.Api | **sin instrumentar** | — |
| **Total (excluyendo migraciones EF)** | **76,1 %** | **57,1 %** |
| Total (incluyendo migraciones EF) | 89,6 % | 57,1 % |

**Nota metodológica.** La cifra histórica de «>85 % global» que declaraban los documentos del proyecto
**incluye las migraciones de EF Core**, que son código generado: `Cauce.Infrastructure` tiene 25 684
líneas instrumentadas, de las cuales 20 065 son migraciones que se ejecutan al crear la base en las
pruebas y cuentan como cubiertas. Excluirlas baja esa capa de 94,8 % a 79,2 %. **La cifra defendible es
76,1 %.** Este bloque no la empeora: todo lo que agrega está entre 94,4 % y 100 %.

**Deuda de instrumentación detectada.** `Cauce.Api` no aparece en ninguno de los cuatro
`coverage.cobertura.xml`. Controllers, middleware y las ocho políticas de rate limit no tienen cifra,
pese a que las 186 pruebas de integración los ejercitan. Registrada en `ESTADO-GAPS-BACKEND.md`; es un
cambio de configuración, no una decisión de arquitectura, así que no lleva acta.

---

## Verificación empírica

Smoke sobre runtime real: seis contenedores healthy, API en Development, token real de Keycloak,
verificación de `audit_logs` contra Postgres. **5 de 6 comprobaciones ejecutadas.**

| # | Comprobación | Resultado |
| --- | --- | --- |
| 1 | `POST /auth/verification-email/resend`, correo existente y correo inexistente | **200 en ambos**, cuerpo vacío. Respuesta uniforme confirmada |
| 2 | El mismo, con correo malformado | **400** `validation_error`, con la clave de `errors` en **camelCase** |
| 3 | `POST /patients/me/nutritionist-assignment` sin JWT | **401** |
| 4 | Canje con código de un nutricionista no disponible | **NO EJECUTADA.** Ver abajo |
| 5 | Login del paciente demo | **200**, objeto `user` completo, `refreshExpiresIn: 2592000` (30 días, `offline_access` aplicado) |
| 6 | Filas nuevas en `audit_logs` | **2 filas** `verification_email_resend_request`, sin actor y con el correo enmascarado |

**Advertencias en el log:** 4 líneas en 56 389. Dos son la deuda conocida del acta A34
(`MinioBucketSeeder`, `NullReferenceException` capturada como warning) y dos las produjo la propia
comprobación 2. Cero `FTL`, cero excepciones no manejadas, los siete workers arrancados.

### Estado real de la auditoría durante el smoke

| Acción | Se auditó |
| --- | --- |
| `verification_email_resend_request`, cuenta existente | **Sí**, sin actor, correo enmascarado |
| `verification_email_resend_request`, cuenta inexistente | **Sí** |
| `login` | **Sí**, con actor resuelto |
| `register` (creación de nutricionista por API key) | **Sí**, con `{"role":"nutritionist","actor":"admin_api_key"}` |
| `password_reset_request`, cuenta existente | **Sí** |
| `password_reset_request`, cuenta **inexistente** | **No.** Hallazgo del smoke → acta **A48** |
| `nutritionist_assignment` | Sin dato: depende de la comprobación 4 |

### Por qué la comprobación 4 quedó abierta

No es alcanzable por vía de la aplicación, y **eso es precisamente la demostración empírica del acta
A47**: no existe ningún camino en el código que lleve una cuenta de nutricionista fuera de `Active`.
Reproducirla exige manipular Postgres directamente, además de registrar un paciente sin asignación y
limpiarle en Keycloak la acción requerida `VERIFY_EMAIL` que bloquea el Direct Grant.

Las tres ramas de `nutritionist_not_available` **sí están cubiertas por pruebas de integración** — de
ahí el 94,4 % del handler. Lo que falta es el ejercicio end-to-end, que se cierra naturalmente en
**Nutritionist-Activation-1**, donde esas ramas pasan a ser alcanzables.

---

## Deudas identificadas

| Acta | Deuda | Se resuelve en |
| --- | --- | --- |
| **A38** | `isInActivePilot` hardcodeado | **Sin bloque asignado.** Bloqueada por dependencia externa: la lista de pacientes del piloto, que debe entregar el Kaelín |
| **A47** | Sin ciclo de vida de cuentas de nutricionista. `Activate`, `Suspend` y `Reactivate` existen en el dominio y no tienen ningún llamador en `src/` | **Nutritionist-Activation-1.** Sin bloqueante externo |
| **A48** | Asimetría en la persistencia de la auditoría de intentos anónimos: `password-reset/request` descarta su fila cuando la cuenta no existe, `verification-email/resend` la persiste | **Por definir.** Recomendado antes de Deployment-1 por la implicancia de trazabilidad |
| — | `Cauce.Api` sin instrumentar para cobertura | Por definir. Cambio de configuración |

---

## Cierre operativo

1. Push de `feature/backend-fix-2-for-mobile`.
2. Esperar CI en verde **antes** de crear el PR.
3. PR contra `develop` con este cuerpo.
4. Merge **con commit de merge**, para preservar la historia del bloque.
5. Tag anotado **`v0.8.0-backend-fix-2`** sobre el commit de merge, y push del tag.
6. Verificar que el tag aparece en GitHub.
7. Borrar `docs/api/openapi-v1.0.0.json`, reemplazado por el v1.1.0.
8. Actualizar la entrada de `CLAUDE.md` a «Backend-Fix-2 cerrado».

> **El tag está citado en cinco documentos** (actas A39, A40 y A41, la sección Backend-Fix-2 de
> `ESTADO-GAPS-BACKEND.md`, y el historial de `MATRIZ-IDENTIDAD.md`). Crearlo con ese nombre exacto es
> lo que evita que esas referencias queden colgando.

**El repo `docs` va por separado:** su commit de trazabilidad se aplica directo sobre `develop`, no
lleva PR, y **el tag va solo en `backend`**.

**Si CI falla tras el push:** pausar, reportar el error exacto, y decidir entre reabrir el bloque,
revertir el tag o abrir un hotfix separado. No forzar el merge.
