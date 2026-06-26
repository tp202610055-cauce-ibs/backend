# CONVENTIONS.md — Cauce Backend

**Versión:** 1.1
**Estado:** Mandatorio para todo código C# nuevo
**Aplicabilidad:** Repo `backend/` (todos los proyectos: Cauce.Domain, Cauce.Application, Cauce.Infrastructure, Cauce.Api, y los tests)

Este documento define el estándar de código del backend. Cualquier código generado (sea a mano, con Claude Code o por revisión) debe cumplirlo. Si una convención necesita cambiar, se actualiza este archivo con incremento de versión (v1.2, v1.3, etc.) y se documenta el cambio en el historial al final.

---

## 1. Lenguaje y framework

- **C# 12** (compatible con .NET 9 SDK 9.0.203).
- **.NET 9.0** como TFM (Target Framework Moniker) en todos los `.csproj`.
- **Nullable reference types**: habilitado (`<Nullable>enable</Nullable>`).
- **Implicit usings**: habilitado (`<ImplicitUsings>enable</ImplicitUsings>`).
- **TreatWarningsAsErrors**: habilitado en `src/` (no en `tests/`).

Configuración base del `.csproj`:

```xml
<PropertyGroup>
  <TargetFramework>net9.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <LangVersion>latest</LangVersion>
</PropertyGroup>
```

---

## 2. Nomenclatura

### 2.1 Tipos, métodos, propiedades públicas

- **PascalCase** para todo lo público: clases, interfaces, structs, enums, records, métodos públicos, propiedades públicas, eventos.
- **Interfaces** prefijadas con `I`: `IUserRepository`, `IKeycloakClient`.
- **Métodos asíncronos** con sufijo `Async`: `GetUserByIdAsync`, `ValidateTokenAsync`. Sin excepciones.
- **Genéricos** con prefijo `T`: `TEntity`, `TRequest`, `TResponse`.

### 2.2 Campos privados, parámetros, variables locales

- **Campos privados de instancia**: `_camelCase` con guion bajo inicial.
- **Constantes privadas**: `UPPER_SNAKE_CASE` solo si son verdaderamente constantes (`const`); si son `static readonly` usar PascalCase.
- **Parámetros de método**: `camelCase` sin guion bajo.
- **Variables locales**: `camelCase`.

```csharp
public class UserService
{
    private readonly IUserRepository _userRepository;
    private const int MAX_LOGIN_ATTEMPTS = 5;
    private static readonly TimeSpan DefaultTokenLifespan = TimeSpan.FromMinutes(15);

    public async Task<User> GetUserAsync(Guid userId)
    {
        var user = await _userRepository.FindByIdAsync(userId);
        return user;
    }
}
```

### 2.3 Archivos y carpetas

- **Archivos**: PascalCase, igual al tipo principal que contienen. `UserService.cs`, `InvitationCode.cs`.
- **Un tipo público por archivo**. Excepciones permitidas: enums pequeños y records auxiliares de un mismo dominio si tienen menos de 20 líneas combinadas.
- **Carpetas**: PascalCase. Organización por módulo de dominio (`Identity/`, `Patients/`), no por tipo técnico (no usar carpetas como `Services/`, `Entities/` en el nivel raíz; van anidadas dentro del módulo).

### 2.4 Namespaces

- **File-scoped namespaces** (C# 10+). No usar el estilo block-scoped.
- Estructura del namespace: `Cauce.<Proyecto>.<Modulo>.<SubModulo>`.

```csharp
namespace Cauce.Domain.Identity;

public sealed class User
{
    // ...
}
```

### 2.5 Nombres de base de datos

- **Tablas**: `snake_case` plural. `users`, `invitation_codes`, `consent_records`.
- **Columnas**: `snake_case` singular. `user_id`, `keycloak_id`, `created_at`.
- **Foreign keys**: `<tabla_referida>_id`. `role_id`, `user_id`, `nutritionist_id`.
- **Índices**: `ix_<tabla>_<columnas>`. `ix_users_email`, `ix_invitation_codes_code`.
- **Restricciones unique**: `ux_<tabla>_<columnas>`. `ux_users_keycloak_id`.
- **Constraints CHECK**: `ck_<tabla>_<descripcion>`. `ck_meal_items_food_xor`.
- **Triggers**: `tr_<tabla>_<evento>`. `tr_audit_logs_reject_modification`.

Mapeo en EF Core: usar paquete `EFCore.NamingConventions` con `.UseSnakeCaseNamingConvention()` para tablas e índices automáticos. **Las columnas se mapean explícitamente** con `.HasColumnName("snake_case")` en `OnModelCreating`. Esto evita ambigüedades de naming entre código y BD.

---

## 3. Formato y estilo

### 3.1 Llaves y bloques

- **Allman braces**: llave de apertura en línea nueva.
- Llaves obligatorias en `if`, `else`, `for`, `foreach`, `while`, `using`, incluso si el cuerpo es de una sola línea.

```csharp
// Correcto
if (user.Status == UserStatus.Suspended)
{
    throw new AccountSuspendedException(user.Id);
}

// Incorrecto
if (user.Status == UserStatus.Suspended) throw new AccountSuspendedException(user.Id);
```

### 3.2 Indentación y espaciado

- **4 espacios** por nivel. No tabs.
- **Longitud máxima de línea**: 120 caracteres. Si una línea excede, partirla en saltos con indentación adicional de 4 espacios.
- **Una línea en blanco** entre métodos, entre grupos lógicos dentro de un método.
- **Sin líneas en blanco** al inicio o final de un bloque `{ }`.

### 3.3 `var` vs tipo explícito

- Usar `var` cuando el tipo es **evidente del lado derecho**.
- Usar tipo explícito cuando el lado derecho no revela el tipo.

```csharp
// var apropiado
var user = new User(email, fullName);
var users = await _repository.GetAllAsync();
var count = users.Count;

// Tipo explícito apropiado
User user = await _repository.FindByIdAsync(id);
int count = ComputeCount();
IReadOnlyList<Recommendation> recommendations = await _service.GetForPatientAsync(patientId);
```

### 3.4 Usings

- **Implicit usings** habilitados, así que `System`, `System.Collections.Generic`, etc. no se importan manualmente.
- Otros usings: agrupar por origen (System, Microsoft, terceros, internos), separados por línea en blanco, ordenados alfabéticamente dentro de cada grupo.
- **No usar `using static`** salvo casos justificados (ej. `using static Math` en un archivo con muchos cálculos numéricos).

---

## 4. Diseño de tipos

### 4.1 Inmutabilidad por defecto

- **Entidades de dominio**: propiedades con `private set` o `init`. Cambios solo vía métodos de dominio que expresan la operación.
- **DTOs y Value Objects**: `record` o clase con propiedades `init`.
- **Configuración**: clases con `init` o records.

```csharp
public sealed class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; }
    public UserStatus Status { get; private set; }

    private User() { } // EF Core

    public User(Guid id, string email, string fullName, int roleId)
    {
        Id = id;
        Email = email;
        FullName = fullName;
        RoleId = roleId;
        Status = UserStatus.PendingActivation;
        CreatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        if (Status != UserStatus.PendingActivation)
        {
            throw new InvalidOperationException("Solo cuentas pendientes pueden activarse.");
        }
        Status = UserStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }
}
```

### 4.2 Sealed por defecto

- Marcar clases como `sealed` salvo que estén diseñadas explícitamente para herencia.
- Esto aplica especialmente a entidades de dominio, services, repositories, DTOs.

### 4.3 Records vs clases

- **Records**: para DTOs, value objects, mensajes (Commands y Queries de MediatR), respuestas API. Cuando el valor importa más que la identidad.
- **Clases**: para entidades de dominio con ciclo de vida y estado mutable controlado, servicios, repositorios.

```csharp
// Record para DTO
public sealed record RegisterPatientRequest(
    string Email,
    string Password,
    string FullName,
    string InvitationCode,
    bool ConsentAccepted,
    string ConsentVersion);

// Clase para entidad
public sealed class PatientProfile
{
    // ...
}
```

### 4.4 Constructores

- **Constructor injection** para todas las dependencias en services, handlers, controllers, repositorios.
- Dependencias declaradas como `readonly`.
- Si hay más de 4-5 parámetros, revisar el diseño: probablemente la clase tiene demasiadas responsabilidades.

```csharp
public sealed class CreatePatientHandler : IRequestHandler<CreatePatientCommand, CreatePatientResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IInvitationCodeRepository _invitationCodeRepository;
    private readonly IKeycloakAdminClient _keycloakClient;
    private readonly IConsentRecordRepository _consentRepository;
    private readonly ILogger<CreatePatientHandler> _logger;

    public CreatePatientHandler(
        IUserRepository userRepository,
        IInvitationCodeRepository invitationCodeRepository,
        IKeycloakAdminClient keycloakClient,
        IConsentRecordRepository consentRepository,
        ILogger<CreatePatientHandler> logger)
    {
        _userRepository = userRepository;
        _invitationCodeRepository = invitationCodeRepository;
        _keycloakClient = keycloakClient;
        _consentRepository = consentRepository;
        _logger = logger;
    }
}
```

### 4.5 Primary constructors (C# 12)

- **Solo en records y clases simples sin lógica de construcción**.
- **No usar en entidades de dominio**: bloquea la inicialización de invariantes.
- **No usar en services con DI**: confunde el reconocimiento de dependencias.

```csharp
// OK en record
public sealed record TokenResponse(string AccessToken, string RefreshToken, int ExpiresIn);

// NO usar en service
// public sealed class UserService(IUserRepository _repository) { ... } // ← evitar
```

---

## 5. Manejo de errores

### 5.1 Excepciones de dominio

- Crear excepciones de dominio específicas, no usar `Exception` genérica.
- Heredar de una base `DomainException` que vive en `Cauce.Domain/Common/Exceptions/`.
- Nombres claros y específicos: `InvitationCodeExpiredException`, `EmailAlreadyRegisteredException`, no `BadRequestException`.

```csharp
namespace Cauce.Domain.Common.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
    protected DomainException(string message, Exception inner) : base(message, inner) { }
}

namespace Cauce.Domain.Identity.Exceptions;

public sealed class InvitationCodeExpiredException : DomainException
{
    public Guid CodeId { get; }

    public InvitationCodeExpiredException(Guid codeId)
        : base($"El código de invitación {codeId} ha expirado.")
    {
        CodeId = codeId;
    }
}
```

### 5.2 Result pattern (preferido sobre excepciones para flujos esperados)

Para flujos donde el fallo es esperado y forma parte del contrato (ej. login fallido, validación de invitation code), usar `Result<T>` en lugar de excepciones.

```csharp
namespace Cauce.Domain.Common;

public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }

    private Result(bool success, T? value, string? error)
    {
        IsSuccess = success;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);
}
```

Las excepciones quedan reservadas para errores **inesperados** o **violaciones de invariantes de dominio** (corrupción de datos, estados imposibles).

### 5.3 Validaciones

- Validaciones de input (formato, presencia, longitud) van en `FluentValidation` validators.
- Validaciones de invariantes de dominio van **dentro de la entidad**, en sus constructores o métodos de dominio.
- Nunca duplicar validaciones entre capas: si FluentValidation ya valida que el email tiene formato válido, el constructor de `User` no lo revalida (asume input ya validado).

---

## 6. Documentación de código (XML doc comments)

### 6.1 Obligatoriedad

- **Todo tipo público** (class, interface, struct, enum, record) debe tener `<summary>` en español.
- **Todo método público** debe tener `<summary>` y `<param>` para cada parámetro, `<returns>` si devuelve valor, `<exception>` para excepciones documentadas que el llamador debe esperar.
- **Propiedades públicas** con nombre no obvio deben tener `<summary>`. Una propiedad `public string Email { get; }` no necesita doc; una propiedad `public decimal PortionSizeGrams { get; }` sí.
- **Métodos privados complejos** (>20 líneas o con lógica no trivial) deben tener doc breve.

### 6.2 Estilo

- Comentarios en español, prosa formal, frase completa con punto final.
- Sin marketing ("amazing", "powerful"): describe qué hace, no opinión.
- Documenta el "qué" y el "por qué", no el "cómo" (eso lo dice el código).

```csharp
/// <summary>
/// Representa el código de invitación generado por un nutricionista para
/// vincular a un nuevo paciente con su consulta. Tiene vigencia de 72 horas
/// y es de uso único.
/// </summary>
public sealed class InvitationCode
{
    /// <summary>
    /// Marca el código como utilizado por el paciente especificado. Cambia el
    /// estado a Used, registra el momento de uso, y asocia el paciente al código.
    /// </summary>
    /// <param name="patientId">Identificador del paciente que consumió el código.</param>
    /// <exception cref="InvitationCodeAlreadyUsedException">
    /// Si el código ya había sido consumido previamente.
    /// </exception>
    /// <exception cref="InvitationCodeExpiredException">
    /// Si el código superó su ventana de vigencia de 72 horas.
    /// </exception>
    public void MarkAsUsedBy(Guid patientId)
    {
        // ...
    }
}
```

### 6.3 Comentarios inline

- Usar `//` para explicar **por qué** el código hace algo no obvio, no **qué** hace.
- Evitar comentarios redundantes que repiten lo que el código ya dice.
- TODO / FIXME / NOTE con formato consistente: `// TODO(trigo): descripción`. Los TODO sin owner deben evitarse.

---

## 7. Async / Await

- **Métodos async devuelven `Task` o `Task<T>`**, nunca `void` salvo en event handlers.
- **Nunca `async void`** en código de aplicación.
- **Nunca `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`**: causa deadlocks. Si necesitas correr async desde sync, hay un problema de diseño.
- **Pasar `CancellationToken`** en todos los métodos async que hacen I/O. Propagar el token recibido del controller hasta el repositorio.
- **`ConfigureAwait(false)`** en libraries (Domain, Application, Infrastructure). En Api no es necesario porque ASP.NET Core no tiene SynchronizationContext.

```csharp
public async Task<User> GetUserAsync(Guid userId, CancellationToken cancellationToken)
{
    var user = await _repository.FindByIdAsync(userId, cancellationToken)
        .ConfigureAwait(false);
    return user;
}
```

---

## 8. LINQ y colecciones

- **Preferir métodos LINQ explícitos** sobre query syntax: `.Where(x => x.Active).ToList()` en vez de `from x in items where x.Active select x`.
- **Materializar explícitamente**: `.ToList()`, `.ToArray()`, `.ToHashSet()` antes de iterar múltiples veces.
- **Devolver `IReadOnlyList<T>`** desde repositorios y queries, no `List<T>` ni `IEnumerable<T>`.
- **Evitar múltiples enumeraciones** de un `IEnumerable<T>` que viene de fuera del scope.

```csharp
// Buena práctica
public async Task<IReadOnlyList<Meal>> GetMealsForPatientAsync(Guid patientId, CancellationToken ct)
{
    return await _context.Meals
        .Where(m => m.PatientId == patientId)
        .OrderByDescending(m => m.ClientCreatedAt)
        .ToListAsync(ct);
}
```

---

## 9. Entity Framework Core

### 9.1 DbContext

- **Un único `CauceDbContext`** en `Cauce.Infrastructure/Persistence/`.
- **No exponer `DbContext` ni `DbSet<T>` fuera de Infrastructure**. Repositorios encapsulan.
- **Sin lazy loading**: usar `Include` explícito o queries proyectivas con `Select`.
- **Tracking off por defecto en queries**: `.AsNoTracking()` en lecturas que no requieren update.

### 9.2 Configuraciones de entidades

- **Una clase por entidad** en `Cauce.Infrastructure/Persistence/Configurations/`, implementando `IEntityTypeConfiguration<T>`.
- Aplicar todas en `OnModelCreating` con `modelBuilder.ApplyConfigurationsFromAssembly(typeof(CauceDbContext).Assembly)`.
- **Mapeo explícito** de tablas, columnas, índices, FKs, CHECK constraints, comportamientos de delete (`OnDelete`).

```csharp
public sealed class InvitationCodeConfiguration : IEntityTypeConfiguration<InvitationCode>
{
    public void Configure(EntityTypeBuilder<InvitationCode> builder)
    {
        builder.ToTable("invitation_codes");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("code_id");

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion(new EnumToStringConverter<InvitationCodeStatus>())
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.NutritionistId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

### 9.3 Migraciones

- Una migración por cambio incremental. No agrupar múltiples cambios no relacionados.
- **Revisar el SQL generado** antes de aplicar (`dotnet ef migrations script`).
- Nombres descriptivos: `AddInvitationCodes`, `AddIndexOnUserEmail`, no `Update1`.
- **No editar migraciones aplicadas**. Si hay un error, generar una nueva migración correctiva.

---

## 10. Inyección de dependencias

- Toda dependencia se registra en `Cauce.Api/Program.cs` o en extensiones de `IServiceCollection` por módulo (`AddIdentityModule`, `AddPatientsModule`).
- **Lifetimes**:
  - `AddScoped`: por request HTTP. Default para repositorios, services, handlers.
  - `AddSingleton`: solo para configuraciones, factories, clients HTTP de larga vida.
  - `AddTransient`: para tipos pequeños sin estado.
- Registrar la interfaz, no la implementación concreta: `services.AddScoped<IUserRepository, UserRepository>()`.

---

## 11. Logging

- `ILogger<T>` inyectado por constructor, T es la clase donde se loguea.
- **Nunca `Console.WriteLine`** en código de aplicación.
- **Niveles**:
  - `LogTrace`: detalle de debugging, off en Production.
  - `LogDebug`: información útil durante desarrollo.
  - `LogInformation`: flujos normales que valen para auditoría no-clínica.
  - `LogWarning`: condiciones inesperadas que no detienen el flujo.
  - `LogError`: fallos que el usuario percibe.
  - `LogCritical`: fallos sistémicos que requieren intervención.
- **Structured logging**: usar templates con placeholders, nunca interpolación.

```csharp
// Correcto
_logger.LogInformation("Patient {PatientId} registered with invitation code {CodeId}",
    patientId, codeId);

// Incorrecto: rompe structured logging
_logger.LogInformation($"Patient {patientId} registered with invitation code {codeId}");
```

- **NO loguear PII**: nombres completos, emails, datos clínicos, contenido de comidas, síntomas. Si necesitas referencia, usa IDs o hashes.

---

## 12. Tests

### 12.1 Estructura

- **Espejo de `src/`**: si existe `Cauce.Application/Identity/UseCases/RegisterPatientHandler.cs`, existe `tests/Cauce.Application.Tests/Identity/UseCases/RegisterPatientHandlerTests.cs`.
- **Una clase de test por clase productiva**.

### 12.2 Naming de tests

- Patrón: `MethodName_StateUnderTest_ExpectedBehavior`. En español o inglés, consistente dentro del archivo.

```csharp
[Fact]
public async Task Handle_InvitationCodeExpired_ReturnsFailureResult()
{
    // Arrange
    // Act
    // Assert
}

[Fact]
public async Task Handle_ValidRequest_PersistsUserAndConsent()
{
    // ...
}
```

### 12.3 Estructura AAA

- **Arrange / Act / Assert** explícito, separado por comentarios o líneas en blanco.
- **Un assert principal por test**. Múltiples asserts solo si verifican el mismo concepto.

### 12.4 Mocking

- `Moq` o `NSubstitute` (elegir uno y mantenerlo consistente en todo el proyecto). Recomendación: `NSubstitute` por sintaxis más limpia.
- **Mock solo las dependencias externas** del SUT (System Under Test), no las internas.

### 12.5 Tests de integración

- Etiqueta `[Trait("Category", "Integration")]` para poder filtrarlos: `dotnet test --filter Category=Integration`.
- Usar `Testcontainers` para Postgres real. No usar SQLite in-memory: las diferencias semánticas con Postgres causan falsos positivos.

---

## 13. Commits

### 13.1 Conventional Commits con scope

Patrón: `<type>(<scope>): <description>`

**Types permitidos**:
- `feat`: nueva funcionalidad
- `fix`: corrección de bug
- `refactor`: cambio de código sin cambiar comportamiento
- `test`: agregar o modificar tests
- `docs`: cambios en documentación
- `chore`: tareas de mantenimiento (deps, config, build)
- `perf`: mejora de performance
- `style`: formato, espacios, sin cambio funcional

**Scopes para Cauce backend**:
- `identity`, `patients`, `clinical-registry`, `recommendations`, `auditing`
- `infra` para infraestructura cross-cutting (Program.cs, DI, middleware base)
- `domain` para cambios en Cauce.Domain Common
- `db` para migrations

### 13.2 Ejemplos

```
feat(identity): add invitation code domain entity with expiration logic
feat(identity): add register patient endpoint
fix(clinical-registry): correct 4h window calculation in symptom association
refactor(infra): extract jwt validation into separate service
test(identity): add integration tests for password reset flow
docs(conventions): add section 14 on configuration patterns
chore(deps): bump EF Core to 9.0.5
db(identity): add migration for invitation_codes table
```

### 13.3 Mensaje del commit

- Descripción en presente imperativo, minúscula inicial, sin punto final.
- Cuerpo opcional para detalles (separado por línea en blanco). Hasta 72 caracteres por línea en el cuerpo.
- Footer con referencias a issues si aplica: `Refs: #US-01`.

---

## 14. Configuración y secretos

### 14.1 appsettings

- `appsettings.json`: defaults para Production.
- `appsettings.Development.json`: overrides para development local.
- `appsettings.Testing.json`: overrides para tests de integración.
- **Nunca commitear secretos** en appsettings. Solo placeholders o valores no sensibles.

### 14.2 User Secrets (development)

Para development local, usar `dotnet user-secrets` para variables sensibles:

```bash
cd src/Cauce.Api
dotnet user-secrets init
dotnet user-secrets set "Keycloak:ClientSecret" "el-secret-real"
dotnet user-secrets set "ConnectionStrings:Cauce" "Host=localhost;..."
```

### 14.3 Environment variables (production)

En production, todas las configuraciones sensibles vienen de variables de entorno con prefijo `CAUCE_`:

```
CAUCE_Keycloak__ClientSecret=...
CAUCE_ConnectionStrings__Cauce=...
```

ASP.NET Core mapea `__` a niveles de JSON automáticamente.

### 14.4 Strong-typed configuration

Configuraciones se exponen vía clases POCO con `IOptions<T>`, no leyendo `IConfiguration` directamente en services.

```csharp
public sealed class KeycloakOptions
{
    public required string Authority { get; init; }
    public required string Realm { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
}

// En Program.cs
builder.Services.Configure<KeycloakOptions>(builder.Configuration.GetSection("Keycloak"));

// En un service
public sealed class KeycloakAdminClient
{
    private readonly KeycloakOptions _options;

    public KeycloakAdminClient(IOptions<KeycloakOptions> options)
    {
        _options = options.Value;
    }
}
```

---

## 15. Seguridad

### 15.1 Inputs

- Toda entrada del usuario se valida en el `Validator` de FluentValidation antes de llegar al handler.
- Validar longitud máxima de strings siempre (riesgo de DoS).
- Sanitizar inputs que se van a usar en HTML (no aplica al backend salvo en reportes PDF).

### 15.2 Outputs

- **Nunca exponer entidades de dominio directamente** en respuestas API. Mapear a DTOs específicos.
- **Nunca exponer**: passwords (no las almacenamos), tokens en logs, IDs internos de Keycloak en respuestas (excepto el propio del usuario autenticado).

### 15.3 SQL

- **Solo via EF Core o parametrizado**. Nunca concatenar SQL.
- Si hay raw SQL necesario, usar `FromSqlInterpolated` o `ExecuteSqlInterpolated` (parametrizado automáticamente).

### 15.4 Autorización

- Cada controller / action que requiere auth lleva `[Authorize(Roles = "patient")]` o `[Authorize(Roles = "nutritionist")]` explícito.
- Endpoints públicos llevan `[AllowAnonymous]` explícito (mejor visible que asumido).
- Validación de ownership en handlers: un paciente solo puede ver sus propias comidas, no las de otros.

---

## 16. Patrones aplicados en el proyecto

### 16.1 CQRS con MediatR

- **Commands**: modifican estado, devuelven `Result<T>` o respuesta minimal.
- **Queries**: leen estado, no tienen efectos secundarios.
- Naming: `RegisterPatientCommand`, `GetPatientProfileQuery`.
- Handlers en `Cauce.Application/<Modulo>/UseCases/`.

### 16.2 Repository pattern

- Una interfaz por agregado en `Cauce.Application/<Modulo>/Repositories/`.
- Implementación EF Core en `Cauce.Infrastructure/<Modulo>/Repositories/`.
- Métodos enfocados en el dominio: `GetByEmailAsync`, `FindActiveByPatientIdAsync`, no `GetAll` genérico.

### 16.3 Unit of Work

- El `DbContext` actúa como Unit of Work. No agregar un `IUnitOfWork` extra salvo necesidad clara.
- `SaveChangesAsync` se llama explícitamente en el handler, no en cada repositorio.

---

## 17. Lo que NO se hace

- `async void` (salvo event handlers).
- `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`.
- Concatenación de SQL.
- Lanzar `Exception` o `ApplicationException` directamente.
- Throw + catch sin re-lanzar (silenciar excepciones sin razón).
- Static state mutable.
- Singletons que mantienen estado de request.
- `Console.WriteLine` en código de aplicación.
- Comentarios de marketing.
- Métodos > 50 líneas sin justificación.
- Clases > 300 líneas sin justificación.
- Métodos con > 5 parámetros sin justificación (considerar parameter object).
- Magic numbers o strings sin nombre.

---

## 18. Lo que SÍ se hace siempre

- Inmutabilidad por defecto.
- Sealed por defecto.
- Constructor injection con readonly.
- Pasar CancellationToken en async I/O.
- Validar inputs en validators dedicados.
- Documentar tipos y métodos públicos en español.
- Commits Conventional con scope.
- Tests para nueva funcionalidad.
- Migration por cambio de modelo.
- Logging structured con placeholders.

---

## 19. Cómo aplicar estas convenciones

### 19.1 Para código nuevo (Claude Code u otro generador)

Estas convenciones son input obligatorio del prompt. Claude Code lee `CLAUDE.md` que referencia este `CONVENTIONS.md`. Si el código generado las viola, se rechaza y se regenera con el feedback específico.

### 19.2 Para revisión

Antes de hacer merge:
1. `dotnet build` sin warnings ni errores.
2. `dotnet format --verify-no-changes` pasa.
3. `dotnet test` pasa.
4. Lectura humana contra este documento.

### 19.3 Para refactor de código existente

Si encuentras código que no cumple, regístralo como TODO y prioriza el refactor según impacto. No hacer refactor masivo sin justificación de bug o feature relacionada.

---

## Historial de versiones

| Versión | Fecha | Cambios |
| --- | --- | --- |
| 1.0 | 2026-05-XX | Versión inicial durante MVP previo |
| 1.1 | 2026-06-23 | Reescrito para post-MVP: Keycloak en vez de ASP.NET Identity, integración con DEC-B3-XX, secciones 14 (config), 15 (seguridad), 16 (patrones) ampliadas |
