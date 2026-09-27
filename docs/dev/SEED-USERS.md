# Usuarios sembrados en Development

**Ámbito:** solo entorno **Development** (`ASPNETCORE_ENVIRONMENT=Development`). La cadena de sembrado
(`RunDevelopmentSeedAsync`) corre al arranque bajo `IsDevelopment()`; las pruebas de integración usan el
perfil `Testing` y **no** ven estos usuarios. En Production estas secciones de configuración no existen y el
sembrado queda deshabilitado.

Estas credenciales son de **desarrollo local**, no secretos de producción. Sirven para validar los flujos de
la app móvil (Flutter) y del portal (React) sin depender del flujo de invitación por nutricionista ni de la
verificación por correo. Todas usan el dominio `@cauce.local`, que no existe fuera de esta red: un correo
real nunca llega a un tercero.

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
| **Contraseña** | `Portal#2026` (**permanente**, sin required actions, desde el acta A68) |
| **Rol (realm Keycloak)** | `nutritionist` |
| **Estado local** | `Active` |
| **Entra por** | `POST /api/v1/auth/portal/login` (portal web) |
| **Seeder / config** | `DevAdminSeeder` · sección `DevAdmin` en `appsettings.Development.json` |

Es la cuenta de CP016, CP045 y CP072. Tiene asignado al paciente demo.

> **Bases sembradas antes del acta A68.** La cuenta nacía con la contraseña temporal `Demo123!`, y el seeder
> no toca una cuenta que ya existe. Hay que correr una sola vez el paso de la sección
> [Puesta a punto en Windows](#puesta-a-punto-en-windows-portal-listo-1), punto 3.

---

## Cuentas de QA del portal (acta A68)

Sembradas por `QaNutritionistsSeeder` (sección `QaNutritionists` de `appsettings.Development.json`), con el
patrón de `DemoPatientSeeder`: solo Development, idempotente por correo, reutiliza el usuario de Keycloak si
ya existe.

| Cuenta | Contraseña | Keycloak | Backend | Qué prueba |
|---|---|---|---|---|
| `nutricionista.inactivo@cauce.local` | `Portal#2026` | **deshabilitada** | `Suspended` | CP017: con la contraseña correcta, el portal responde el mismo 401 `invalid_credentials`. La auditoría registra `account_disabled` |
| `nutricionista.pendiente@cauce.local` | *(ninguna)* | habilitada, sin credencial | `PendingActivation` | Cuenta pendiente de activación: cualquier contraseña da 401, con causa `pending_activation`. No se le envía el correo de activación |

`nutricionista.inactivo` queda `Suspended` y no `Inactive` porque el backend no tiene una transición a
`Inactive` para nutricionistas. Para el portal, las dos son igual de "no activas".

---

## Cómo iniciar sesión en el portal (sin portal)

```bash
curl -i -X POST http://localhost:5074/api/v1/auth/portal/login \
  -H "Content-Type: application/json" \
  -d '{"email":"nutricionista.demo@cauce.local","password":"Portal#2026"}'
```

La respuesta trae `accessToken`, `expiresIn`, `tokenType` y `user`, y un header
`Set-Cookie: cauce_portal_rt=…; path=/api/v1/auth/portal; samesite=strict; httponly`. El refresh token no
viaja en el cuerpo. Para renovar, reenviar esa cookie con el header `X-Cauce-Portal: 1` a
`POST /api/v1/auth/portal/refresh`.

---

## Puesta a punto en Windows (Portal listo 1)

Tres pasos que se hacen **una sola vez** por máquina, con el stack levantado (Docker Desktop abierto y los
contenedores `cauce-keycloak` y `cauce-postgres` corriendo). No hace falta borrar nada: los usuarios se
conservan.

**1. Abrir PowerShell en la carpeta `infrastructure`.** Tecla Windows, escribir `PowerShell`, Enter. En la
ventana azul, pegar (clic derecho pega) y Enter:

```powershell
cd "$HOME\Documents\github_flavio_trigueros\academicos\proyecto_final_cauce\infrastructure"
```

No debería mostrar nada; la línea siguiente empieza con `PS C:\Users\...\infrastructure>`.

**2. Actualizar el cliente del portal en Keycloak y guardar su secret en el backend.** Pegar estas tres
líneas juntas y Enter:

```powershell
docker cp keycloak/apply-portal-client.sh cauce-keycloak:/tmp/apply-portal-client.sh
$secret = (docker exec cauce-keycloak bash -c "tr -d '\r' < /tmp/apply-portal-client.sh | bash" | Select-Object -Last 1)
dotnet user-secrets set "Keycloak:WebPortalClientSecret" $secret --project ..\backend\src\Cauce.Api
```

Lo que debería verse:

```
Logging into http://localhost:8080 as user admin of realm master
Successfully saved Keycloak:WebPortalClientSecret to the secret store.
```

El secret no se muestra en pantalla: se guarda directo. Si aparece `Error: No such container: cauce-keycloak`,
el stack no está levantado. Repetir el paso no rompe nada: conserva el mismo secret.

> **Si se prefiere ver el secret.** En `http://localhost:8081/admin`, entrar con el usuario admin del `.env`
> de `infrastructure`, elegir el realm `cauce` arriba a la izquierda, ir a **Clients** → `cauce-web-portal` →
> pestaña **Credentials** → **Client Secret**, y copiarlo con el ícono de copiar. Nunca pulsar
> **Regenerate** sin volver a guardarlo en el backend.

**3. Solo si la base es anterior al acta A68: dejar permanente la contraseña del nutricionista demo.** Pegar
estas dos líneas juntas y Enter:

```powershell
docker cp keycloak/dev-demo-nutritionist-password.sh cauce-keycloak:/tmp/dev-demo.sh
docker exec cauce-keycloak bash -c "tr -d '\r' < /tmp/dev-demo.sh | bash"
```

Debería terminar con `Listo: nutricionista.demo@cauce.local tiene contraseña permanente y ninguna acción
pendiente.` Si la cuenta no existe todavía, avisa que el seeder la va a crear ya permanente.

**4. Reiniciar el backend** (`dotnet run --project src/Cauce.Api`, o Run en el IDE). Al arrancar crea las dos
cuentas de QA si no existen. En el log deberían aparecer `Seeded disabled QA nutritionist` y `Seeded pending
QA nutritionist` la primera vez.

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
