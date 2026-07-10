# Usuarios sembrados en Development

**Ámbito:** solo entorno **Development** (`ASPNETCORE_ENVIRONMENT=Development`). La cadena de sembrado
(`RunDevelopmentSeedAsync`) corre al arranque bajo `IsDevelopment()`; las pruebas de integración usan el
perfil `Testing` y **no** ven estos usuarios. En Production estas secciones de configuración no existen y el
sembrado queda deshabilitado.

Estas credenciales son de **desarrollo local**, no secretos de producción. Sirven para validar los flujos de
la app móvil (Flutter) y del portal (React) sin depender del flujo de invitación por nutricionista ni de la
verificación por correo.

---

## Paciente demo — `paciente.demo@cauce.local`

| Campo | Valor |
|---|---|
| **Email / usuario** | `paciente.demo@cauce.local` |
| **Contraseña** | `Paciente.Demo2026!` (**permanente**, sin required actions) |
| **Rol (realm Keycloak)** | `patient` |
| **Correo verificado** | sí |
| **Cliente OIDC** | `cauce-mobile` (público, Direct Access Grants ON) |
| **Seeder / config** | `DemoPatientSeeder` · sección `DemoPatient` en `appsettings.Development.json` |

**Perfil clínico:** nac. 1990-05-15 · `Female` · 62.5 kg · 162 cm · subtipo `IbsD` · onboarding completo ·
`IsInActivePilot = true` · consentimiento vigente · asignado al nutricionista demo (si está sembrado).

**Historial mínimo (timestamps relativos a la fecha de arranque):**
- **5 comidas** en los últimos 7 días (2 desayunos, 2 almuerzos, 1 cena; una con un alimento personalizado).
- **3 síntomas** en los últimos 7 días (dolor abdominal · distensión · flatulencia; uno asociado a una comida
  dentro de la ventana de 4 h).
- **1 IBS-SSS de línea base** hace 14 días, total **220** (moderado) — deja margen para medir la reducción de
  ≥50 puntos del endpoint primario.
- **1 nota clínica** del paciente, asociada a un síntoma.

**Escenarios que habilita:** login por Direct Access Grants (Mobile-1) · `GET /patients/me` con perfil
completo · `GET /meals`, `/symptoms`, `/ibs-sss/latest`, `/history` con datos · `POST /meals` idempotente
(Mobile-3) · `GET /patients/me/summary` (US28) · export y PDF de consentimiento (US25/US01 CA04).

Ver acta [`A30-dev-seed-patient.md`](../../../docs/decisions/A30-dev-seed-patient.md).

---

## Nutricionista demo — `nutricionista.demo@cauce.local`

| Campo | Valor |
|---|---|
| **Email / usuario** | `nutricionista.demo@cauce.local` |
| **Contraseña** | `Demo123!` (**temporal** — required action `UPDATE_PASSWORD`) |
| **Rol (realm Keycloak)** | `nutritionist` |
| **Seeder / config** | `DevAdminSeeder` · sección `DevAdmin` en `appsettings.Development.json` |

> ⚠️ Por la contraseña temporal, este usuario **no** puede iniciar sesión por Direct Access Grants hasta
> cambiarla (required action `UPDATE_PASSWORD`). Para probar el portal web con un nutricionista logueable,
> cambiar su contraseña a permanente en la consola de Keycloak (`http://localhost:8081/admin`, realm `cauce`)
> o vía password reset.

---

## Cómo obtener un token del paciente demo (Direct Access Grants)

```bash
curl -s -X POST http://localhost:8081/realms/cauce/protocol/openid-connect/token \
  -d "client_id=cauce-mobile" -d "grant_type=password" \
  -d "username=paciente.demo@cauce.local" -d "password=Paciente.Demo2026!" -d "scope=openid"
```

El `access_token` devuelto lleva `realm_access.roles: ["patient"]`. Úsalo como `Authorization: Bearer <token>`
contra `http://localhost:5074/api/v1/...`.

> Nota: en Development el rate limiting está **desactivado** (`RateLimiting:Enabled=false`), así que no hay
> 429 durante el desarrollo local. En Production/Testing permanece activo.

---

## Resultado del smoke test v0.6.2

**Fecha del smoke:** viernes 10 de julio de 2026.
**Ambiente:** Development local (Windows 11, Docker Desktop, .NET 9).
**Usuario ejercitado:** `paciente.demo@cauce.local` / `Paciente.Demo2026!`.
**GUIDs:** `user_id` `79974080-cfbb-4ce8-b003-4e80e7e9e84f`, `keycloak_id` (`sub`) `b8ebd09c-3bb3-4e7b-90dd-a55124bae0fd`.

**Resultado de los 10 pasos ejercitados:**

| Paso | Endpoint / verificación | Resultado |
|---|---|---|
| 5 | POST token Keycloak Direct Grant | 201, JWT emitido |
| 6 | Claim de rol en el JWT | `patient` presente en `realm_access.roles` |
| 7 | `GET /api/v1/patients/profile` | 200, IbsD, Female, 36 años, BMI 23.81 |
| 8 | `GET /api/v1/meals` | 200, `totalCount = 5` |
| 9 | `POST /api/v1/meals` con Idempotency-Key nuevo | 201 en 0.70 s |
| 10 | `POST /api/v1/meals` con la misma Idempotency-Key | 200 en 0.02 s, mismo `mealId` |
| 11 | `GET /api/v1/symptoms` | 200, `totalCount = 3` |
| 12 | `GET /api/v1/ibs-sss/latest` | 200, Baseline, `totalScore = 220`, Moderate |
| 13 | Reinicio de la API sin `down -v`, idempotencia del seed | conteos singulares en Postgres |
| 14 | Aislamiento Testing (verificado por código) | `UseEnvironment("Testing")` impide la ejecución del seed |

**Conteos post-smoke en Postgres (paciente demo):**
- `users`: 1
- `patient_profiles`: 1
- `ibs_sss_assessments`: 1 (baseline)
- `symptoms`: 3
- `meals`: 8 (5 del seed + 3 de las pruebas del smoke, dejados intencionalmente como artefactos)
- `clinical_notes`: 1

**Nueve capas de infra resueltas para llegar al verde:** ver actas A31 a A37 en `../../../docs/decisions/`
y la sección "Setup del entorno de desarrollo local" de `CLAUDE.md`.

**Deuda residual identificada:** los buckets MinIO `clinical-reports` y `patient-exports` no se crean
automáticamente por un `NullReferenceException` en `MinioBucketSeeder` (acta A34, diferido a Mobile-5).
