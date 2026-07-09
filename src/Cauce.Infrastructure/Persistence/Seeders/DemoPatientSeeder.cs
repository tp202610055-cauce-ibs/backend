using System.Security.Cryptography;
using System.Text;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using Cauce.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Persistence.Seeders;

/// <summary>
/// Seeder de desarrollo que provisiona un paciente de prueba completo (identidad Keycloak + perfil
/// clínico + historial mínimo) para validar el happy-path autenticado de la app móvil sin depender del
/// flujo de invitación por nutricionista ni de la verificación por correo. Es idempotente y solo se
/// ejecuta en Development (la cadena <see cref="SeedingExtensions.RunDevelopmentSeedAsync"/> corre bajo
/// <c>IsDevelopment()</c>, y además está gateado por <c>DemoPatient:Enabled</c>). Ver acta A30.
///
/// <para>A diferencia de <see cref="DevAdminSeeder"/>, establece una contraseña <b>permanente</b>
/// (<see cref="IKeycloakAdminClient.ResetPasswordAsync"/>) y el correo verificado, de modo que el paciente
/// puede iniciar sesión por Direct Access Grants inmediatamente.</para>
///
/// <para>Las filas de auditoría generadas al sembrar en tablas con trigger (perfil, comidas, síntomas,
/// IBS-SSS, asignación) quedan con <c>actor_user_id = NULL</c>: en un seeder no hay usuario HTTP, la GUC
/// <c>cauce.actor_user_id</c> queda vacía y el trigger la resuelve con <c>NULLIF(..., '')::uuid</c>. Es el
/// comportamiento correcto (no fue una acción humana atribuible) y solo aplica a datos DEV. Ver acta A30.</para>
/// </summary>
public sealed class DemoPatientSeeder
{
    private readonly CauceDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly IKeycloakAdminClient _keycloakAdminClient;
    private readonly IOptions<DemoPatientOptions> _options;
    private readonly ILogger<DemoPatientSeeder> _logger;

    /// <summary>
    /// Inicializa el seeder con sus dependencias.
    /// </summary>
    public DemoPatientSeeder(
        CauceDbContext context,
        IUserRepository userRepository,
        IKeycloakAdminClient keycloakAdminClient,
        IOptions<DemoPatientOptions> options,
        ILogger<DemoPatientSeeder> logger)
    {
        _context = context;
        _userRepository = userRepository;
        _keycloakAdminClient = keycloakAdminClient;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Provisiona el paciente de prueba y su historial clínico si está habilitado y aún no existe. La
    /// persistencia es atómica: identidad local, perfil, consentimiento e historial se guardan en un solo
    /// <c>SaveChanges</c>; si algo falla, no queda estado parcial.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var options = _options.Value;
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.Email))
        {
            return;
        }

        if (await _userRepository.ExistsByEmailAsync(options.Email, ct).ConfigureAwait(false))
        {
            _logger.LogInformation("Demo patient already present; skipping seed (idempotent no-op).");
            return;
        }

        var now = DateTime.UtcNow;
        var patientRoleId = await _userRepository.GetRoleIdAsync(UserRoles.Patient, ct).ConfigureAwait(false);

        // Identidad en Keycloak: reutiliza el usuario si un intento previo lo dejó creado; contraseña
        // permanente (sin required actions) para habilitar el Direct Access Grant. La password nunca se loguea.
        var existing = await _keycloakAdminClient.FindByEmailAsync(options.Email, ct).ConfigureAwait(false);
        var keycloakId = existing?.Id
            ?? await _keycloakAdminClient
                .CreateUserAsync(options.Email, options.FullName, UserRoles.Patient, requireEmailVerification: false, ct)
                .ConfigureAwait(false);
        await _keycloakAdminClient.ResetPasswordAsync(keycloakId, options.Password, ct).ConfigureAwait(false);

        // Cuenta local: verificada, activa e inscrita en el piloto activo (US26 CA02 comprobable).
        var user = User.CreatePatient(Guid.NewGuid(), keycloakId, options.Email, options.FullName, patientRoleId);
        user.VerifyEmail();
        user.EnrollInActivePilot();
        await _userRepository.AddAsync(user, ct).ConfigureAwait(false);

        // Consentimiento informado (US01 CA04 / US28). Texto de demostración; el hash es su SHA-256 real.
        const string consentText = "Consentimiento informado de demostración para el entorno de desarrollo de Cauce.";
        var consentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(consentText))).ToLowerInvariant();
        _context.Add(ConsentRecord.Capture(Guid.NewGuid(), user.Id, "1.0", consentHash, "127.0.0.1", now.AddDays(-40)));

        // Perfil clínico. Estatura en centímetros (162), no metros. Onboarding completo (hay línea base).
        var profile = PatientProfile.Create(
            Guid.NewGuid(),
            user.Id,
            new DateOnly(1990, 5, 15),
            BiologicalSex.Female,
            62.5m,
            162m,
            IbsSubtype.IbsD,
            DateOnly.FromDateTime(now.AddYears(-2)),
            medications: null,
            now.AddDays(-40));
        profile.CompleteOnboarding(now.AddDays(-14));
        _context.Add(profile);

        // Vínculo con el nutricionista de prueba si existe (DevAdminSeeder corre antes en la cadena).
        var nutritionistRoleId = await _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, ct).ConfigureAwait(false);
        var nutritionist = await _context.Set<User>()
            .Where(candidate => candidate.RoleId == nutritionistRoleId && candidate.Status == UserStatus.Active)
            .OrderBy(candidate => candidate.CreatedAt)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (nutritionist is not null)
        {
            _context.Add(NutritionistPatient.Establish(Guid.NewGuid(), nutritionist.Id, user.Id, invitationCodeId: null, now.AddDays(-40)));
        }

        // IBS-SSS de línea base hace 14 días, puntaje total 220 (60+40+50+40+30) → categoría moderada,
        // deja margen para medir la reducción de ≥50 puntos del endpoint primario.
        _context.Add(IbsSssAssessment.Submit(
            Guid.NewGuid(), user.Id, AssessmentType.Baseline, cycleNumber: 0,
            painSeverity: 60, painFrequency: 40, bloatingSeverity: 50, bowelHabitsDissatisfaction: 40,
            lifeInterference: 30, now.AddDays(-14)));

        // Historial de comidas/síntomas: requiere alimentos reales del catálogo TPCA-CENAN ya sembrado.
        var foods = await _context.Set<FoodItem>()
            .Where(food => food.IsActive)
            .OrderBy(food => food.Name)
            .Take(8)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (foods.Count < 3)
        {
            _logger.LogWarning(
                "Demo patient seed: food catalog has fewer than 3 active items; persisting identity/profile only, without meal/symptom history.");
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("Seeded demo patient {UserId} (without clinical history).", user.Id);
            return;
        }

        // Alimento personalizado (para que al menos una comida lo referencie).
        var customFood = CustomFood.Create(Guid.NewGuid(), user.Id, "Ensalada demo", 200m, now.AddDays(-6));
        customFood.AddIngredient(foods[0].Id, 100m);
        customFood.AddIngredient(foods[1].Id, 100m);
        _context.Add(customFood);

        // 5 comidas en los últimos 7 días: 2 desayunos, 2 almuerzos, 1 cena.
        var breakfast1 = BuildMeal(user.Id, MealTime.Breakfast, now.AddDays(-6), 8, now,
            new MealItemInput(foods[2].Id, null, 150m, MeasurementUnit.Grams),
            new MealItemInput(foods[3].Id, null, 50m, MeasurementUnit.Grams));

        var lunchWithCustom = BuildMeal(user.Id, MealTime.Lunch, now.AddDays(-5), 13, now,
            new MealItemInput(null, customFood.Id, 1m, MeasurementUnit.Units),
            new MealItemInput(foods[4].Id, null, 120m, MeasurementUnit.Grams));

        var dinner1 = BuildMeal(user.Id, MealTime.Dinner, now.AddDays(-4), 20, now,
            new MealItemInput(foods[5 % foods.Count].Id, null, 200m, MeasurementUnit.Grams),
            new MealItemInput(foods[2].Id, null, 80m, MeasurementUnit.Grams),
            new MealItemInput(foods[3].Id, null, 40m, MeasurementUnit.Grams));

        var breakfast2 = BuildMeal(user.Id, MealTime.Breakfast, now.AddDays(-2), 8, now,
            new MealItemInput(foods[0].Id, null, 120m, MeasurementUnit.Grams),
            new MealItemInput(foods[1].Id, null, 60m, MeasurementUnit.Grams));

        var lunch2 = BuildMeal(user.Id, MealTime.Lunch, now.AddDays(-1), 13, now,
            new MealItemInput(foods[2].Id, null, 130m, MeasurementUnit.Grams),
            new MealItemInput(foods[4].Id, null, 90m, MeasurementUnit.Grams),
            new MealItemInput(foods[5 % foods.Count].Id, null, 70m, MeasurementUnit.Grams));

        _context.AddRange(breakfast1, lunchWithCustom, dinner1, breakfast2, lunch2);

        // 3 síntomas en los últimos 7 días. El dominio modela intensidad 1–100 (no "severity"): se mapean
        // los niveles descritos a intensidades representativas. Uno se asocia a una comida en la ventana de 4h.
        var painAfterBreakfast = Symptom.Report(
            Guid.NewGuid(), Guid.NewGuid(), user.Id, SymptomType.AbdominalPain, intensity: 60,
            breakfast2.ClientCreatedAt.AddHours(2), breakfast2.ClientCreatedAt.AddHours(2), now);
        painAfterBreakfast.AssociateWithMeal(breakfast2.Id, now);

        var bloating = Symptom.Report(
            Guid.NewGuid(), Guid.NewGuid(), user.Id, SymptomType.Bloating, intensity: 35,
            now.AddDays(-3).Date.AddHours(19), now.AddDays(-3).Date.AddHours(19), now);

        var flatulence = Symptom.Report(
            Guid.NewGuid(), Guid.NewGuid(), user.Id, SymptomType.Flatulence, intensity: 30,
            now.AddDays(-1).Date.AddHours(16), now.AddDays(-1).Date.AddHours(16), now);

        _context.AddRange(painAfterBreakfast, bloating, flatulence);

        // Nota clínica del paciente (el dominio no modela notas del nutricionista): asociada a un síntoma,
        // con timestamp coherente (poco después del síntoma).
        _context.Add(ClinicalNote.Attach(
            Guid.NewGuid(), user.Id, mealId: null, symptomId: bloating.Id,
            "Distensión leve tras la cena; la registro para seguimiento del piloto.",
            now.AddDays(-3).Date.AddHours(20)));

        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        _logger.LogInformation(
            "Seeded demo patient {UserId} with clinical history (5 meals, 1 custom food, 3 symptoms, 1 IBS-SSS baseline, 1 clinical note).",
            user.Id);
    }

    private static Meal BuildMeal(
        Guid patientId,
        MealTime mealTime,
        DateTime day,
        int hourOfDay,
        DateTime serverUtcNow,
        params MealItemInput[] items)
    {
        var at = day.Date.AddHours(hourOfDay);
        return Meal.Register(Guid.NewGuid(), Guid.NewGuid(), patientId, mealTime, at, at, items, serverUtcNow);
    }
}
