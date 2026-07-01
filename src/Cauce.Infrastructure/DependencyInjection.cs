using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.Configuration;
using Cauce.Domain.Recommendations.Services;
using Cauce.Infrastructure.Auditing;
using Cauce.Infrastructure.Caching;
using Cauce.Infrastructure.ClinicalRegistry;
using Cauce.Infrastructure.Email;
using Cauce.Infrastructure.Identity;
using Cauce.Infrastructure.Patients;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Persistence.Repositories;
using Cauce.Infrastructure.Persistence.Seeders;
using Cauce.Infrastructure.Recommendations.Engines;
using Cauce.Infrastructure.Recommendations.Llm;
using Cauce.Infrastructure.Recommendations.Readers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Cauce.Infrastructure;

/// <summary>
/// Métodos de extensión para registrar los servicios de la capa de infraestructura
/// en el contenedor de inyección de dependencias.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra el contexto de base de datos, las opciones, los repositorios, los
    /// servicios de identidad y correo, y los seeders de la capa de infraestructura.
    /// </summary>
    /// <param name="services">Colección de servicios a configurar.</param>
    /// <param name="configuration">Configuración de la aplicación.</param>
    /// <returns>La misma colección de servicios para encadenamiento.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<CauceDbContext>(options =>
        {
            options
                .UseNpgsql(configuration.GetConnectionString("Cauce"))
                .UseSnakeCaseNamingConvention();
        });

        // Opciones.
        services.Configure<KeycloakOptions>(configuration.GetSection(KeycloakOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<ConsentDocumentOptions>(configuration.GetSection(ConsentDocumentOptions.SectionName));
        services.Configure<DevAdminOptions>(configuration.GetSection(DevAdminOptions.SectionName));
        services.Configure<KeyDbOptions>(configuration.GetSection(KeyDbOptions.SectionName));

        // Servicios transversales.
        services.AddHttpContextAccessor();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Servicios de identidad y correo.
        services.AddSingleton<IConsentService, ConsentService>();
        services.AddSingleton<IPasswordResetTokenGenerator, PasswordResetTokenGenerator>();
        services.AddSingleton<ITemporaryPasswordGenerator, TemporaryPasswordGenerator>();
        services.AddSingleton<IClientUrlProvider, ClientUrlProvider>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        // Servicios del módulo de pacientes.
        services.AddSingleton<IBmiCalculator, BmiCalculator>();

        // Servicios del módulo de registro clínico.
        services.AddSingleton<IFodmapAggregator, FodmapAggregator>();
        services.AddSingleton<IIbsSssScorer, IbsSssScorer>();

        // Almacén de idempotencia respaldado por KeyDB. AbortOnConnectFail=false evita que
        // un KeyDB no disponible rompa el arranque; el almacén degrada (fail-open).
        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<KeyDbOptions>>().Value;
            var configurationOptions = ConfigurationOptions.Parse(options.ConnectionString);
            configurationOptions.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(configurationOptions);
        });
        services.AddSingleton<IIdempotencyStore, KeyDbIdempotencyStore>();

        // Repositorios del módulo de identidad.
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IInvitationCodeRepository, InvitationCodeRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IConsentRecordRepository, ConsentRecordRepository>();

        // Repositorios del módulo de pacientes.
        services.AddScoped<IPatientProfileRepository, PatientProfileRepository>();
        services.AddScoped<IAllergyRepository, AllergyRepository>();
        services.AddScoped<IPatientAllergyRepository, PatientAllergyRepository>();
        services.AddScoped<INutritionistPatientRepository, NutritionistPatientRepository>();

        // Repositorios del módulo de registro clínico.
        services.AddScoped<IFoodItemRepository, FoodItemRepository>();
        services.AddScoped<ICustomFoodRepository, CustomFoodRepository>();
        services.AddScoped<IMealRepository, MealRepository>();
        services.AddScoped<ISymptomRepository, SymptomRepository>();
        services.AddScoped<IClinicalNoteRepository, ClinicalNoteRepository>();
        services.AddScoped<IIbsSssAssessmentRepository, IbsSssAssessmentRepository>();

        // Módulo de recomendaciones.
        services.AddOptions<RecommendationsOptions>()
            .Bind(configuration.GetSection(RecommendationsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<RecommendationsOptions>, RecommendationsOptionsValidator>();

        // Selección del motor por configuración (DEC-B4-01).
        services.AddSingleton<IRecommendationEngine>(serviceProvider =>
        {
            var recommendationsOptions = serviceProvider.GetRequiredService<IOptions<RecommendationsOptions>>().Value;
            return recommendationsOptions.EngineKind switch
            {
                "Rule" => ActivatorUtilities.CreateInstance<FodmapRuleRecommendationEngine>(serviceProvider),
                "Onnx" => ActivatorUtilities.CreateInstance<OnnxRecommendationEngine>(serviceProvider),
                _ => throw new InvalidOperationException(
                    $"EngineKind inválido '{recommendationsOptions.EngineKind}'. Se esperaba 'Rule' u 'Onnx'.")
            };
        });

        // Orquestador LLM y respaldo.
        services.AddSingleton<RecommendationGuardrailsValidator>();
        services.AddSingleton<FallbackExplanationProvider>();
        services.AddHttpClient<IExplanationOrchestrator, OllamaExplanationOrchestrator>();

        // Servicios de dominio y lectores cross-module.
        services.AddSingleton<AutoApprovalGuard>();
        services.AddSingleton<AllergyHeuristicMatcher>();
        services.AddScoped<IRecommendationRepository, RecommendationRepository>();
        services.AddScoped<IModelVersionRepository, ModelVersionRepository>();
        services.AddScoped<IPatientClinicalHistoryReader, PatientClinicalHistoryReader>();
        services.AddScoped<IPatientAllergyReader, PatientAllergyReader>();
        services.AddScoped<IPatientProfileReader, PatientProfileReader>();
        services.AddScoped<IFoodCatalogReader, FoodCatalogReader>();

        // Seeders.
        services.AddScoped<UserRolesSeeder>();
        services.AddScoped<AllergiesSeeder>();
        services.AddScoped<FoodItemsSeeder>();
        services.AddScoped<DevAdminSeeder>();
        services.AddScoped<RecommendationsModelVersionsSeeder>();

        // Cliente de administración de Keycloak (cliente HTTP tipado).
        services.AddHttpClient<IKeycloakAdminClient, KeycloakAdminClient>();

        return services;
    }
}
