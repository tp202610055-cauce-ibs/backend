# Cauce — Backend

API REST que sostiene Cauce, un sistema de recomendaciones dietéticas para pacientes con Síndrome de Intestino Irritable (SII). El proyecto nace de una tesis de pregrado en colaboración con el Complejo Hospitalario Guillermo Kaelín de la Fuente (EsSalud Lima Sur), con un piloto clínico planeado de 20 a 50 pacientes.

Esta es la pieza del servidor. Hay otras dos: una app móvil en Flutter para los pacientes y un portal web en React para los nutricionistas, ambas en repos hermanos. Las tres conversan vía esta API.

## Qué hace el backend

Tres cosas principales, ordenadas por peso:

**Gestiona los datos clínicos del paciente.** Perfil clínico, alergias, registro diario de comidas, registro de síntomas, evaluaciones IBS-SSS cada dos semanas. Todo persiste en PostgreSQL con auditoría completa por requerimiento de la Ley N° 29733 de Protección de Datos Personales.

**Genera recomendaciones dietéticas con IA, supervisadas por humano.** Un modelo XGBoost entrenado sobre datos sintéticos clínicamente validados produce recomendaciones nutricionales. Antes de que el paciente las vea, un nutricionista las revisa y las aprueba, rechaza o ajusta. Es el flujo HITL (Human-In-The-Loop), no automatización ciega. El modelo corre como ONNX en el servidor, sin salir nunca al exterior. La narrativa final que lee el paciente se genera con un LLM local (Llama 3.1 8B) por las mismas razones de soberanía de datos.

**Sincroniza el trabajo offline del móvil.** La app del paciente debe funcionar sin red, porque la realidad de Lima Sur no garantiza conexión constante. Los registros se guardan localmente y se sincronizan cuando hay conexión, de forma idempotente. El backend valida y persiste sin duplicar.

## Stack

- **.NET 9** (SDK 9.0.203) con C# 12
- **ASP.NET Core** para la API REST
- **Entity Framework Core 9** con provider Npgsql
- **PostgreSQL 16** como base de datos principal
- **Keycloak 25** como servidor de identidad (OIDC + JWT)
- **KeyDB** para caché y colas asíncronas (Streams)
- **MinIO** para almacenamiento de objetos (PDFs de reportes, artefactos ONNX)
- **Ollama** corriendo Llama 3.1 8B Q4 para el orquestador LLM
- **ONNX Runtime** para inferencia del modelo de recomendaciones

Todo lo de infraestructura corre en Docker. El backend en sí corre nativo en Windows durante development por simplicidad; se dockeriza en producción.

## Arquitectura

Clean Architecture con cuatro proyectos, regla de dependencia hacia adentro:

```
src/
├── Cauce.Domain/           # Entidades, value objects, lógica de negocio pura
├── Cauce.Application/      # Use cases (CQRS con MediatR), interfaces, validators
├── Cauce.Infrastructure/   # EF Core, repos, Keycloak client, ONNX, Ollama
└── Cauce.Api/              # Controllers, middleware, composition root
```

`Domain` no depende de nada. `Application` solo de `Domain`. `Infrastructure` de los dos anteriores. `Api` de todos. Es un monolito modular: una sola solución, organización interna por dominio funcional (Identity, Patients, ClinicalRegistry, Recommendations, Auditing), no por tipo técnico.

Si quieres entender por qué cada decisión está tomada de la forma que está, hay tres documentos que conviene leer en este orden:

1. **`CLAUDE.md`** — guía de contexto del proyecto y mapeo de qué va dónde
2. **`CONVENTIONS.md`** — convenciones de código C# que aplican siempre
3. **`../docs/decisions/DECISIONS-BLOCK-3.md`** — las nueve decisiones técnicas vinculantes (tokens, CORS, rate limiting, idempotency, auditoría, etc.)

Los diagramas viven en el repo `docs/`: ERD completo (`diagrama_erd_total.dbml`), diagrama de clases (`diagrama_clases_total.puml`), arquitectura C4 (`diagramas_c4_v_2.dsl`).

## Correr esto localmente

Asumiendo que ya tienes .NET 9, Docker Desktop y Git instalados:

### 1. Levantar la infraestructura

La infraestructura vive en el repo hermano `infrastructure/`. Desde ahí:

```bash
cd ../infrastructure
docker compose --profile auth up -d
```

Eso levanta Postgres, Adminer, Keycloak y Mailpit. Si necesitas el resto (KeyDB, MinIO para reportes y modelos), añade `--profile infra`. Para Ollama (cuando trabajes con el LLM), añade `--profile ai`. El README del repo `infrastructure/` tiene los detalles.

Verifica que todo está arriba:

```bash
docker compose ps
```

Deberías ver `cauce-postgres`, `cauce-adminer`, `cauce-keycloak` y `cauce-mailpit` en estado `healthy`.

### 2. Configurar el backend

Volviendo al repo backend:

```bash
cd ../backend
```

User secrets para development (los valores van sin commitear):

```bash
cd src/Cauce.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Cauce" "Host=localhost;Port=5432;Database=cauce_dev;Username=cauce;Password=<tu_password_de_postgres>"
dotnet user-secrets set "Keycloak:Authority" "http://localhost:8081/realms/cauce"
dotnet user-secrets set "Keycloak:ClientId" "cauce-backend"
dotnet user-secrets set "Keycloak:ClientSecret" "<el_secret_que_regeneraste_en_keycloak_admin>"
cd ../..
```

El secret de Keycloak es el que regeneraste en el panel admin cuando configuraste el realm. Si no lo anotaste, ve a Keycloak → realm cauce → Clients → cauce-backend → Credentials → Regenerate.

### 3. Aplicar migraciones

Con la infraestructura arriba y los secrets configurados:

```bash
dotnet ef database update -p src/Cauce.Infrastructure -s src/Cauce.Api
```

Esto crea las tablas en la base `cauce_dev` y aplica los triggers de inmutabilidad de `audit_logs`.

### 4. Arrancar la API

```bash
dotnet run --project src/Cauce.Api
```

La API queda escuchando en `http://localhost:5074`. Swagger en `http://localhost:5074/swagger`.

## Tests

```bash
# Todos
dotnet test

# Solo unitarios (rápidos)
dotnet test tests/Cauce.Domain.Tests tests/Cauce.Application.Tests

# Solo integración (lentos, levantan Postgres con Testcontainers)
dotnet test --filter Category=Integration
```

## Estructura del repo

```
backend/
├── Cauce.sln
├── CLAUDE.md                  # Contexto del proyecto para herramientas de IA
├── CONVENTIONS.md             # Convenciones de código (mandatorias)
├── README.md                  # Este archivo
├── .gitignore
├── src/
│   ├── Cauce.Domain/          # Entidades, enums, lógica pura
│   ├── Cauce.Application/     # Handlers MediatR, DTOs, validators
│   ├── Cauce.Infrastructure/  # EF Core, Keycloak client, persistencia
│   └── Cauce.Api/             # Controllers, Program.cs, middleware
└── tests/
    ├── Cauce.Domain.Tests/
    ├── Cauce.Application.Tests/
    └── Cauce.Api.IntegrationTests/
```

## Convenciones de trabajo

**Ramas:** trunk-based. `main` es la rama estable, `develop` es donde se integra. Features y fixes se mergean a `develop` y de ahí van a `main` cuando hacen sentido.

**Commits:** Conventional Commits con scope. Ejemplos: `feat(identity): add invitation code validator`, `fix(clinical-registry): correct 4h window`, `db(patients): add migration for allergies`. Más detalles en `CONVENTIONS.md` sección 13.

**Pull requests:** no aplica en este momento porque el equipo somos dos personas trabajando en ramas distintas. Cuando se sume gente o se quiera formalizar, se activa el flujo.

## Sobre el piloto clínico

El despliegue al servidor del Hospital Kaelín está previsto como fase posterior a la aprobación del Comité de Ética del hospital, en coordinación con el área de TI de EsSalud. La arquitectura está dockerizada precisamente para que el deploy sea reproducible y portable: la misma imagen que corre en mi laptop es la que correrá allá.

Mientras tanto, el sistema vive en local. Para la sustentación de la tesis se hace demo desde acá.

## Quién más toca este repo

**Mirian Contreras Paquita** se encarga del entrenamiento del modelo XGBoost que alimenta el motor de recomendaciones. Su trabajo es en Python y vive en otro lugar; el resultado (el archivo `.onnx`) se sube a este sistema vía el CLI command de provisioning de modelos (ver DEC-B3-08).

Cualquier cambio sobre `Cauce.Application/Recommendations/Inference/` o `Cauce.Infrastructure/Recommendations/Inference/` requiere coordinación con ella para mantener la tolerancia <0.001 entre la inferencia ONNX y la Python original.

## Licencia

Por definir. Tesis de pregrado UPC, código en desarrollo activo.
