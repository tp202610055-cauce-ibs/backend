# Acta A46: Swashbuckle CLI como herramienta local para regenerar el snapshot OpenAPI

**Estado:** Aprobada, aplicada en Backend-Fix-2 Fase 5
**Fecha:** 2026-09-07
**Aprobado por:** Flavio Eduardo Trigueros Chumacero
**Aplicabilidad:** Herramientas de desarrollo del repo `backend`. No toca código de producción ni el contrato de API.

---

## Contexto

La Fase 5 del bloque Backend-Fix-2 debe regenerar el snapshot OpenAPI como `openapi-v1.1.0.json`, con
los dos endpoints nuevos del bloque. Al buscar cómo se genera, **no se encontró ningún mecanismo
versionado en el repositorio**:

| Se buscó | Resultado |
| --- | --- |
| Script en `tools/` | Solo `generate_dummy_onnx.py`, ajeno al tema |
| Manifiesto de herramientas locales | `.config/dotnet-tools.json` no existía |
| Argumento CLI en `Program.cs` | No hay ninguno que emita el documento |
| Paquete de generación | Solo `Swashbuckle.AspNetCore` 7.2.*, sin su CLI |

El snapshot vigente se generó, presumiblemente, levantando la API y descargando
`/swagger/v1/swagger.json`, que solo se expone en Development. Ese camino tiene tres costos:

- Depende de que los tres user-secrets del perfil local estén configurados (actas A33, A35, A37).
- Arrancar en Development ejecuta los seeders de desarrollo y los siete workers contra la base local.
- No deja nada versionado: la próxima regeneración vuelve a depender de que alguien recuerde el
  procedimiento.

Este bloque es la segunda vez que el proyecto necesita regenerar el snapshot, y no será la última:
cada endpoint nuevo lo desactualiza.

## Decisión

Adoptar **`Swashbuckle.AspNetCore.Cli` como dotnet tool local**, declarada en
`.config/dotnet-tools.json`.

La regeneración pasa a ser:

```bash
dotnet tool restore
dotnet build src/Cauce.Api
ASPNETCORE_ENVIRONMENT=Development dotnet swagger tofile \
  --output docs/api/openapi-v1.1.0.json \
  src/Cauce.Api/bin/Debug/net9.0/Cauce.Api.dll v1
```

La herramienta carga el ensamblado compilado y le pide el documento al generador, sin abrir un puerto y
sin tocar la base de datos.

**`ASPNETCORE_ENVIRONMENT=Development` no es opcional.** Sin esa variable la herramienta corre en
Production, donde no se cargan los user-secrets; la construcción del host falla por configuración
ausente y `HostFactoryResolver` cae a la ruta heredada de `Startup`, produciendo un error engañoso:

```
System.InvalidOperationException: A type named 'StartupProduction' or 'Startup'
could not be found in assembly 'Cauce.Api'.
```

El mensaje no menciona la configuración faltante, así que conviene tenerlo registrado: la aplicación usa
instrucciones de nivel superior y no tiene ni debe tener una clase `Startup`.

**La herramienta se detiene en `builder.Build()`.** No ejecuta lo que viene después en `Program.cs`, de
modo que ni los seeders de desarrollo ni los siete workers llegan a arrancar.

## Consecuencias

- **Dependencia nueva**, declarada y con versión fija en el manifiesto, alineada con la de
  `Swashbuckle.AspNetCore` que ya usa el proyecto. Es reproducible: `dotnet tool restore` la instala
  igual en cualquier máquina.
- **Automatizable.** Deployment-1 la necesitará para publicar la documentación de la API sin
  intervención manual, y sirve tal cual en CI para verificar que el snapshot commiteado coincide con el
  código.
- **No se toca `Program.cs`.** El pipeline de la aplicación queda igual, y Swagger sigue expuesto solo
  en Development.
- El procedimiento queda documentado en `CLAUDE.md` cuando la Fase 6 lo actualice a v2.2.0.

## Alternativas descartadas

| Alternativa | Motivo del descarte |
| --- | --- |
| Levantar la API y descargar el spec | Depende del entorno local, dispara seeders y workers contra la base de desarrollo, y no deja nada versionado |
| Agregar un argumento CLI a `Program.cs` que emita el documento | Mezcla una preocupación de herramientas con el arranque de la aplicación, y hay que mantenerlo a mano |
| Diferir el snapshot | La Fase Final declara `openapi-v1.1.0.json` como criterio de cierre del bloque |

## Referencias

- `.config/dotnet-tools.json`
- `docs/api/openapi-v1.1.0.json`
- Fases 5 y Final del prompt Backend-Fix-2.
- Acta A44, que registra la otra decisión de infraestructura del bloque.
