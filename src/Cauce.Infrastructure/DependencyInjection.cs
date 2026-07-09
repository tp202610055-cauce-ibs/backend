using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Application.Common.Interfaces.Outbox;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Common.Interfaces.Reports;
using Cauce.Application.Common.Interfaces.Storage;
using Cauce.Application.Recommendations.Configuration;
using Cauce.Domain.Recommendations.Services;
using Cauce.Infrastructure.Auditing;
using Cauce.Infrastructure.Caching;
using Cauce.Infrastructure.ClinicalRegistry;
using Cauce.Infrastructure.ClinicalRegistry.Readers;
using Cauce.Infrastructure.Email;
using Cauce.Infrastructure.Identity;
using Cauce.Infrastructure.Notifications;
using Cauce.Infrastructure.Notifications.Senders;
using Cauce.Infrastructure.Outbox;
using Cauce.Infrastructure.Patients;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Persistence.Repositories;
using Cauce.Infrastructure.Persistence.Seeders;
using Cauce.Infrastructure.Recommendations.Engines;
using Cauce.Infrastructure.Recommendations.Llm;
using Cauce.Infrastructure.Recommendations.Readers;
using Cauce.Infrastructure.Reports;
using Cauce.Infrastructure.Storage;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Minio;
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
        // El interceptor propaga el actor autenticado a la variable de sesión de PostgreSQL para los
        // triggers de auditoría (capa 4). Es scoped y se resuelve por contexto (acta A6).
        services.AddScoped<AuditActorContextInterceptor>();
        services.AddDbContext<CauceDbContext>((serviceProvider, options) =>
        {
            options
                .UseNpgsql(configuration.GetConnectionString("Cauce"))
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(serviceProvider.GetRequiredService<AuditActorContextInterceptor>());
        });

        // Opciones.
        services.Configure<KeycloakOptions>(configuration.GetSection(KeycloakOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<ConsentDocumentOptions>(configuration.GetSection(ConsentDocumentOptions.SectionName));
        services.Configure<DevAdminOptions>(configuration.GetSection(DevAdminOptions.SectionName));
        services.Configure<KeyDbOptions>(configuration.GetSection(KeyDbOptions.SectionName));
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));
        services.Configure<MinioOptions>(configuration.GetSection(MinioOptions.SectionName));
        services.Configure<ReportOptions>(configuration.GetSection(ReportOptions.SectionName));

        // Servicios transversales.
        services.AddHttpContextAccessor();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<AuditActorResolver>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Servicios de identidad y correo.
        services.AddSingleton<IConsentService, ConsentService>();
        services.AddSingleton<IPasswordResetTokenGenerator, PasswordResetTokenGenerator>();
        services.AddSingleton<ITemporaryPasswordGenerator, TemporaryPasswordGenerator>();
        services.AddSingleton<IClientUrlProvider, ClientUrlProvider>();
        services.AddSingleton<IConsentPdfRenderer, QuestPdfConsentRenderer>();
        services.AddSingleton<SmtpMessageDispatcher>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddHttpClient<IKeycloakTokenClient, KeycloakTokenClient>();

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
        services.AddScoped<IIbsSssAssessmentScheduleRepository, IbsSssAssessmentScheduleRepository>();
        services.AddScoped<IGlossaryRepository, GlossaryRepository>();

        // Módulo de recomendaciones.
        services.AddOptions<RecommendationsOptions>()
            .Bind(configuration.GetSection(RecommendationsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<RecommendationsOptions>, RecommendationsOptionsValidator>();

        // El motor de regla se registra como tipo concreto para poder inyectarlo como respaldo del
        // motor ONNX (fallback silencioso cuando el modelo no está disponible, TS07).
        services.AddSingleton<FodmapRuleRecommendationEngine>();

        // Selección del motor por configuración (DEC-B4-01).
        services.AddSingleton<IRecommendationEngine>(serviceProvider =>
        {
            var recommendationsOptions = serviceProvider.GetRequiredService<IOptions<RecommendationsOptions>>().Value;
            return recommendationsOptions.EngineKind switch
            {
                "Rule" => serviceProvider.GetRequiredService<FodmapRuleRecommendationEngine>(),
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
        services.AddScoped<IRecommendationSupportingDataReader, RecommendationSupportingDataReader>();
        services.AddScoped<IFoodSuggestionsReader, FoodSuggestionsReader>();
        services.AddScoped<ICustomFoodAllergenChecker, CustomFoodAllergenChecker>();
        services.AddScoped<IbsSssScheduleProcessor>();

        // Módulo de auditoría, outbox y notificaciones (Prompt 5).
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddSingleton<OutboxEventTypeRegistry>();
        services.AddScoped<OutboxBatchProcessor>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationScheduler, NotificationScheduler>();
        services.AddScoped<NotificationBatchProcessor>();
        services.AddScoped<INotificationSender, SmtpEmailNotificationSender>();
        RegisterFcmSender(services, configuration);

        // Almacenamiento de objetos (MinIO) y reportes clínicos.
        services.AddSingleton<IMinioClient>(serviceProvider =>
        {
            var minioOptions = serviceProvider.GetRequiredService<IOptions<MinioOptions>>().Value;
            return new MinioClient()
                .WithEndpoint(minioOptions.Endpoint)
                .WithCredentials(minioOptions.AccessKey, minioOptions.SecretKey)
                .WithSSL(minioOptions.UseSsl)
                .Build();
        });
        services.AddScoped<IObjectStorage, MinioObjectStorage>();
        services.AddScoped<IClinicalReportDataReader, ClinicalReportDataReader>();
        services.AddScoped<IClinicalReportMetadataRepository, ClinicalReportMetadataRepository>();
        services.AddSingleton<IbsSssChartRenderer>();
        services.AddScoped<IPdfReportGenerator, PdfReportGenerator>();
        services.AddScoped<IClinicalDataExporter, ClinicalDataExporter>();

        // Seeders.
        services.AddScoped<UserRolesSeeder>();
        services.AddScoped<AllergiesSeeder>();
        services.AddScoped<FoodItemsSeeder>();
        services.AddScoped<GlossaryTermsSeeder>();
        services.AddScoped<DevAdminSeeder>();
        services.Configure<Cauce.Infrastructure.Identity.DemoPatientOptions>(
            configuration.GetSection(Cauce.Infrastructure.Identity.DemoPatientOptions.SectionName));
        services.AddScoped<DemoPatientSeeder>();
        services.AddScoped<RecommendationsModelVersionsSeeder>();
        services.AddScoped<MinioBucketSeeder>();

        // Cliente de administración de Keycloak (cliente HTTP tipado).
        services.AddHttpClient<IKeycloakAdminClient, KeycloakAdminClient>();

        return services;
    }

    /// <summary>
    /// Registra el remitente de notificaciones push: el falso en desarrollo y pruebas, o el real de
    /// Firebase Cloud Messaging cuando <c>Notifications:Fcm:UseFake</c> es <see langword="false"/>.
    /// </summary>
    /// <param name="services">Colección de servicios.</param>
    /// <param name="configuration">Configuración de la aplicación.</param>
    private static void RegisterFcmSender(IServiceCollection services, IConfiguration configuration)
    {
        var fcmOptions = configuration
            .GetSection(NotificationOptions.SectionName)
            .Get<NotificationOptions>()?.Fcm ?? new Cauce.Infrastructure.Notifications.FcmOptions();

        if (fcmOptions.UseFake)
        {
            // Singleton para que las pruebas observen la misma instancia (SentMessages); el fake no
            // tiene estado por-petición.
            services.AddSingleton<FakeFcmSender>();
            services.AddSingleton<INotificationSender>(sp => sp.GetRequiredService<FakeFcmSender>());
            return;
        }

        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<NotificationOptions>>().Value.Fcm;
            var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
            {
                Credential = string.IsNullOrWhiteSpace(options.CredentialsPath)
                    ? GoogleCredential.GetApplicationDefault()
                    : GoogleCredential.FromFile(options.CredentialsPath)
            });
            return FirebaseMessaging.GetMessaging(app);
        });
        services.AddScoped<INotificationSender, FirebaseCloudMessagingSender>();
    }
}
