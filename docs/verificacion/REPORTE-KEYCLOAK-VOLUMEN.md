# Reporte de Volumen de Keycloak — ¿Sobrevivieron los fixes A35 y A36?

**Ejecutado:** 21 de agosto de 2026, 05:12 UTC (00:12 hora de Lima)
**Modo:** solo lectura. Únicamente `GET` a la Admin API y un `grant_type=password` de smoke test.
**Motivo:** cerrar el bloqueante **B4** de [`REPORTE-VERIFICACION-03.md`](REPORTE-VERIFICACION-03.md).
**Alcance:** determinar si el volumen persistente conservó el client scope `cauce-backend-audience`
con su `oidc-audience-mapper` (acta A35) y el scope `basic` reasignado como default a `cauce-mobile` y
`cauce-web-portal` (acta A36).

---

# VEREDICTO: **SOBREVIVIÓ** (íntegro)

Los dos fixes de consola están presentes y operativos. Verificado por dos vías independientes: la
Admin API de Keycloak y un token real emitido por Direct Access Grant para el paciente demo.

| Fix | Acta | Estado | Verificación |
|---|---|---|---|
| Client scope `cauce-backend-audience` con `oidc-audience-mapper` | A35 | **PRESENTE** | Admin API + `aud` del token real |
| Scope `basic` como default en `cauce-mobile` | A36 | **PRESENTE** | Admin API + claim `sub` del token real |
| Scope `basic` como default en `cauce-web-portal` | A36 | **PRESENTE** | Admin API |
| `cauce-backend-audience` como default en ambos clientes | A35 | **PRESENTE** | Admin API |

**No hay nada que reconstruir.** La sección "qué falta" de este reporte queda vacía.

---

## 0. Estado del entorno y adaptación de los pasos

**Adaptación del paso 1.** El prompt pide `docker compose up -d keycloak keycloak-db`. **No existe un
servicio `keycloak-db`**: Keycloak persiste su configuración en el Postgres compartido
(`infrastructure/docker-compose.yml:84-87`, `KC_DB_URL: jdbc:postgresql://postgres:5432/${KEYCLOAK_DB_NAME}`),
cuya base la crea el script de init `postgres/init/01-create-keycloak-db.sh` (acta A31). Por lo tanto
**el volumen relevante es `cauce-postgres-data`**, no un volumen propio de Keycloak.

**No hizo falta levantar nada.** Trigo ya tenía el stack arriba:

```
cauce-adminer   |adminer:4-standalone            |Up 2 minutes
cauce-keycloak  |quay.io/keycloak/keycloak:25.0  |Up 2 minutes (healthy)
cauce-postgres  |postgres:16-alpine              |Up 3 minutes (healthy)
cauce-minio     |minio/minio:latest              |Up 3 minutes (healthy)
cauce-mailpit   |axllent/mailpit:latest          |Up 3 minutes (healthy)
cauce-keydb     |eqalpha/keydb:alpine            |Up 3 minutes (healthy)
```

No se ejecutó ningún `docker compose up`, `down`, `restart` ni `volume rm`.

**Evidencia de que el volumen no se recreó:**

```
=== VOLUMENES ===
cauce-keydb-data
cauce-minio-data
cauce-postgres-data
cauce_postgres_data
nextcloud_data
nextcloud_db

=== DETALLE cauce-postgres-data ===
Nombre: cauce-postgres-data
Creado: 2026-07-09T13:50:55Z
Mountpoint: /var/lib/docker/volumes/cauce-postgres-data/_data

=== KEYCLOAK ===
Started: 2026-08-21T04:56:21.757227427Z
RestartCount: 0
Created: 2026-07-09T13:50:56.363637464Z
```

El volumen data del **9 de julio de 2026**, un día **anterior** a las actas A35 y A36 (10 de julio).
El contenedor tampoco se recreó (`Created` del 9 de julio, `RestartCount: 0`); solo se reinició hoy.
Es decir, la configuración de consola aplicada el 10 de julio se escribió sobre este mismo volumen y
nunca se borró.

> **Observación menor.** Existe un volumen huérfano `cauce_postgres_data` (con guiones bajos), remanente
> del naming por defecto de Docker Compose antes de que el `docker-compose.yml` fijara
> `name: cauce-postgres-data` (línea 194). No está montado por ningún contenedor. No se tocó.

---

## 1. Salida cruda — Client scopes del realm `cauce`

**Llamada:** `GET /admin/realms/cauce/client-scopes`
**Auth:** admin token del realm `master`, cliente `admin-cli`, usuario `admin` (contraseña redactada,
leída de `infrastructure/.env`).

```
Total de client scopes en el realm: 12
Nombres: acr, address, basic, cauce-backend-audience, email, microprofile-jwt,
         offline_access, phone, profile, role_list, roles, web-origins
```

**El scope `cauce-backend-audience` existe.** Nótese que **no está en `realm.json`**: es exclusivamente
producto del fix de consola de A35.

### Salida cruda del scope (JSON íntegro)

```json
{
    "id": "2714b6fd-bb7a-42d2-90f9-558ffe4866c2",
    "name": "cauce-backend-audience",
    "description": "Adds cauce-backend to the aud claim of tokens",
    "protocol": "openid-connect",
    "attributes": {
        "include.in.token.scope": "false",
        "display.on.consent.screen": "false",
        "gui.order": "",
        "consent.screen.text": ""
    },
    "protocolMappers": [
        {
            "id": "ac56f1a1-4130-4938-8bec-bcb2f86d062a",
            "name": "add-cauce-backend-audience",
            "protocol": "openid-connect",
            "protocolMapper": "oidc-audience-mapper",
            "consentRequired": false,
            "config": {
                "included.client.audience": "cauce-backend",
                "id.token.claim": "false",
                "lightweight.claim": "false",
                "access.token.claim": "true",
                "introspection.token.claim": "true"
            }
        }
    ]
}
```

### Verificación punto por punto del paso 4.a

| Requisito del prompt | Valor encontrado | ¿Cumple? |
|---|---|---|
| Existe scope `cauce-backend-audience` | sí, id `2714b6fd-…` | **SÍ** |
| Tiene protocolMapper `oidc-audience-mapper` | `add-cauce-backend-audience` | **SÍ** |
| `included.client.audience = cauce-backend` | `"cauce-backend"` | **SÍ** |
| `access.token.claim = true` | `"true"` | **SÍ** |

Coincide exactamente con lo que describe el acta A35 ("Included Client Audience = `cauce-backend`,
Add to access token = ON").

---

## 2. Salida cruda — Default client scopes de `cauce-mobile`

**Llamadas:** `GET /admin/realms/cauce/clients?clientId=cauce-mobile` →
`GET /admin/realms/cauce/clients/1cfe0b3c-dcd0-45b8-9af9-eab1e72af8b7/default-client-scopes`

```
id interno: 1cfe0b3c-dcd0-45b8-9af9-eab1e72af8b7
publicClient=True  directAccessGrantsEnabled=True  standardFlowEnabled=True
```

```json
[
  { "id": "59b2a8e2-ffc9-47ab-8936-dc8a5704ea5e", "name": "web-origins" },
  { "id": "08de7e85-0c00-4441-8683-14aa5dcf5eff", "name": "profile" },
  { "id": "bffdb78a-d36d-4455-a5d5-7bd709a0419c", "name": "roles" },
  { "id": "94cf3867-7e43-449d-9fb6-bdda22ea9ca8", "name": "basic" },
  { "id": "2714b6fd-bb7a-42d2-90f9-558ffe4866c2", "name": "cauce-backend-audience" },
  { "id": "0e79e73b-8b67-4574-b8f3-7b8f9fad773f", "name": "email" }
]
```

| Requisito | ¿Cumple? |
|---|---|
| Incluye `basic` | **SÍ** |
| Incluye `cauce-backend-audience` | **SÍ** |

**Contraste con `realm.json:101`**, que declara `["web-origins", "profile", "roles", "email"]` — cuatro
scopes. El realm en ejecución tiene **seis**: los cuatro del archivo más `basic` y
`cauce-backend-audience`. La divergencia entre repo y runtime es exactamente la que documentan A35 y A36.

---

## 3. Salida cruda — Default client scopes de `cauce-web-portal`

**Llamadas:** `GET /admin/realms/cauce/clients?clientId=cauce-web-portal` →
`GET /admin/realms/cauce/clients/8fcdaf01-1558-42da-9b7c-58f6e62d7cf8/default-client-scopes`

```
id interno: 8fcdaf01-1558-42da-9b7c-58f6e62d7cf8
publicClient=False  directAccessGrantsEnabled=False  standardFlowEnabled=True
```

```json
[
  { "id": "59b2a8e2-ffc9-47ab-8936-dc8a5704ea5e", "name": "web-origins" },
  { "id": "08de7e85-0c00-4441-8683-14aa5dcf5eff", "name": "profile" },
  { "id": "bffdb78a-d36d-4455-a5d5-7bd709a0419c", "name": "roles" },
  { "id": "94cf3867-7e43-449d-9fb6-bdda22ea9ca8", "name": "basic" },
  { "id": "2714b6fd-bb7a-42d2-90f9-558ffe4866c2", "name": "cauce-backend-audience" },
  { "id": "0e79e73b-8b67-4574-b8f3-7b8f9fad773f", "name": "email" }
]
```

| Requisito | ¿Cumple? |
|---|---|
| Incluye `basic` | **SÍ** |
| Incluye `cauce-backend-audience` | **SÍ** |

Los scopes son idénticos a los de `cauce-mobile` (mismos ids), como manda A36. Se confirma además que
`directAccessGrantsEnabled=False` y `publicClient=False` en el runtime, igual que en `realm.json`: la
deuda técnica del passthrough de login para el portal web **sigue vigente** y no la toca este reporte.

---

## 4. Salida cruda — Smoke test con el paciente demo

**Llamada:** `POST /realms/cauce/protocol/openid-connect/token`
**Parámetros:** `grant_type=password`, `client_id=cauce-mobile`,
`username=paciente.demo@cauce.local`, `scope=openid`. Contraseña redactada.

```
RESPUESTA OK (tokens redactados)
  token_type         = Bearer
  expires_in         = 900
  refresh_expires_in = 1800
  access_token       = <REDACTADO, 1441 chars>
  refresh_token      = <REDACTADO, 752 chars>
  scope              = openid email profile
```

### Claims decodificados del access token

```
  iss                = http://localhost:8081/realms/cauce
  azp                = cauce-mobile
  typ                = Bearer
  preferred_username = paciente.demo@cauce.local
  email              = paciente.demo@cauce.local
  email_verified     = True
  scope              = openid email profile
  exp                = 1787290031  (UTC: 2026-08-21 05:27:11)
  iat                = 1787289131  (UTC: 2026-08-21 05:12:11)

  aud                = cauce-backend, account
  sub                = b8ebd09c-3bb3-4e7b-90dd-a55124bae0fd
  realm_access.roles = default-roles-cauce, patient, offline_access, uma_authorization
```

### Verificaciones del paso 5

| Requisito | Resultado |
|---|---|
| `aud` incluye `"cauce-backend"` | **SÍ** — `aud = ["cauce-backend", "account"]` |
| Existe el claim `sub` | **SÍ** — `b8ebd09c-3bb3-4e7b-90dd-a55124bae0fd` |
| El rol `patient` está mapeado | **SÍ** — presente en `realm_access.roles` |
| Direct Access Grant funciona para el paciente demo | **SÍ** — 200 con tokens |

**Confirmación adicional de identidad del volumen.** El `sub` obtenido
(`b8ebd09c-3bb3-4e7b-90dd-a55124bae0fd`) es **byte a byte el mismo** que registró el acta A36 al
verificar el fix el 10 de julio. No solo sobrevivió la configuración: sobrevivió **la misma fila de
usuario**. Es prueba concluyente de que el volumen es el original y no una reconstrucción.

Con esto, los dos síntomas que motivaron A35 y A36 quedan descartados en runtime:
- Sin el audience mapper el token traería `aud: "account"` → 401 en endpoints protegidos. **No ocurre.**
- Sin el scope `basic` el token no traería `sub` → 403 en endpoints que resuelven usuario local.
  **No ocurre.**

---

## 5. Parámetros de sesión del realm en runtime

Consultados de paso, porque son insumo directo del GAP 1 (`GET /admin/realms/cauce`):

```
ssoSessionIdleTimeout      = 1800
ssoSessionMaxLifespan      = 2592000
offlineSessionIdleTimeout  = 2592000
accessTokenLifespan        = 900
revokeRefreshToken         = True
refreshTokenMaxReuse       = 0
bruteForceProtected        = True
failureFactor              = 5
```

Coinciden con `realm.json`. Sin sorpresas aquí.

---

## Qué falta reconstruir

**Nada.** El veredicto es SOBREVIVIÓ íntegro, así que no aplica la sección de reconstrucción desde las
actas A35 y A36.

---

## Consecuencias para el GAP 7 y el bloqueante B4

**El GAP 7 sigue ABIERTO.** Que los fixes sobrevivan en la base de datos **no los pone en el repo**.
`infrastructure/keycloak/import/realm.json` sigue con un único commit del 23 de junio de 2026
(`b5079ee`), sin sección `clientScopes` y con `defaultClientScopes` de cuatro elementos. El riesgo que
describe el GAP 7 es idéntico: un `docker compose down -v` pierde ambos fixes en silencio.

**Lo que cambia es el costo del fix, y cambia a favor.** El bloqueante B4 planteaba dos caminos:

| Camino | ¿Aplica? | Costo |
|---|---|---|
| (a) Exportar el realm vivo y persistirlo en el repo | **SÍ, es el camino disponible** | Bajo |
| (b) Reconstruir a mano `clientScopes` y `protocolMappers` desde las actas | Ya no hace falta | — |

**B4 queda cerrado.** El GAP 7 se puede especificar con el camino (a): exportar el realm desde el
contenedor vivo, reconciliar el resultado con el `realm.json` actual y commitear. Se mantiene el
estimado **M** del `ESTADO-GAPS-BACKEND.md`, ahora sin el riesgo de crecer que se anotaba allí.

Dos apuntes para quien escriba ese fix:

1. **La exportación no es trivial en Keycloak 25.** `kc.sh export` con el servidor corriendo en
   `start-dev` requiere cuidado (el export a un solo archivo puede omitir los client scopes si no se
   usan las banderas correctas). Conviene validar el JSON exportado buscando explícitamente
   `oidc-audience-mapper` y `"basic"` antes de commitear. Alternativa más quirúrgica: componer a mano
   la sección `clientScopes` con el JSON crudo que este reporte ya captura en la sección 1, y añadir
   `basic` y `cauce-backend-audience` a los `defaultClientScopes` de ambos clientes.
2. **Sigue sin existir script de re-export** (P7.7 del reporte anterior). El fix debería incluirlo,
   porque el vacío de proceso es la causa estructural del gap, no el archivo desactualizado en sí.

---

## Hallazgo colateral: confirmación empírica de la sorpresa S1

El smoke test devolvió **`refresh_expires_in = 1800`**, es decir **30 minutos**, no 30 días.

Esto confirma empíricamente la sorpresa **S1** del `REPORTE-VERIFICACION-03.md`, que hasta ahora era
una deducción a partir de la configuración. El refresh token está atado a la sesión SSO, cuyo
`ssoSessionIdleTimeout` es 1800 s. El scope `offline_access` —que elevaría el idle a
`offlineSessionIdleTimeout = 2592000`, o sea 30 días— **no se solicitó**: el backend pide solo
`scope=openid` (`src/Cauce.Infrastructure/Identity/KeycloakTokenClient.cs:51`), y la respuesta lo
confirma con `scope = openid email profile`, sin `offline_access`.

> Ojo con una confusión fácil: `offline_access` **sí aparece** en `realm_access.roles` del token. Eso
> es el **rol** por defecto del realm, que solo habilita a pedir el scope. El **scope** no se pidió, y
> es el scope el que determina el tipo de refresh token. Por eso `refresh_expires_in` es 1800 y no
> 2592000.

**Implicación para el GAP 1, ahora con evidencia dura:** construir `POST /auth/refresh` sin tocar esto
deja la sesión muriendo a los 30 minutos de inactividad, y US08 CA02 seguiría sin cumplirse. La
decisión sigue abierta (bloqueante B3): pedir `offline_access` en el login del móvil, o subir
`ssoSessionIdleTimeout` en el realm. **Este reporte no la resuelve ni aplica ningún cambio.**

---

## Reglas respetadas

| Regla | Cumplimiento |
|---|---|
| No modificar `realm.json` | Cumplida. Solo se leyó |
| No aplicar fixes por consola | Cumplida. Solo `GET` a la Admin API, más un `POST` al token endpoint (que no muta configuración) |
| No commitear nada | Cumplida |
| No tocar `src/` ni `tests/` | Cumplida |
| No resetear volúmenes | Cumplida. No se ejecutó `down`, `down -v`, `restart` ni `volume rm` |
| Reportar sin arreglar | Cumplida. Nada falló, y nada se arregló |

Los scripts de consulta se escribieron en el directorio temporal de la sesión, fuera del repositorio.
El único archivo creado dentro del repo es este documento.
