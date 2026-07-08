using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cauce.Infrastructure.Persistence.Seeders;

/// <summary>
/// Seeder idempotente del glosario clínico-nutricional del piloto (US27). Inserta los términos que aún
/// no existen. El contenido está en estado de borrador pendiente de validación clínica con el
/// nutricionista del Complejo Hospitalario Guillermo Kaelín (acta A27).
/// </summary>
public sealed class GlossaryTermsSeeder
{
    private static readonly IReadOnlyList<(string Term, string Patient, string Nutritionist, GlossaryCategory Category)> Catalog = new[]
    {
        // --- Clínicos del SII ---
        ("SII", "Síndrome de Intestino Irritable: una condición del sistema digestivo que causa dolor de barriga, hinchazón y cambios en la forma de ir al baño, sin que haya un daño visible en el intestino.", "Síndrome de Intestino Irritable: trastorno funcional gastrointestinal crónico caracterizado por dolor abdominal recurrente asociado a la defecación y cambios en el hábito intestinal, en ausencia de hallazgos estructurales o bioquímicos.", GlossaryCategory.ClinicalIbs),
        ("Roma IV", "Es la lista de señales que usan los médicos para saber si alguien tiene SII y de qué tipo.", "Criterios diagnósticos vigentes para los trastornos de la interacción intestino-cerebro; definen el SII y sus subtipos (IBS-D, IBS-C, IBS-M, IBS-U) según patrón de dolor y forma de las heces.", GlossaryCategory.ClinicalIbs),
        ("IBS-D", "Tipo de SII en el que predomina la diarrea.", "Subtipo del SII con predominio de diarrea: más del 25 % de las deposiciones son tipo 6-7 de Bristol y menos del 25 % tipo 1-2.", GlossaryCategory.ClinicalIbs),
        ("IBS-C", "Tipo de SII en el que predomina el estreñimiento.", "Subtipo del SII con predominio de estreñimiento: más del 25 % de las deposiciones son tipo 1-2 de Bristol y menos del 25 % tipo 6-7.", GlossaryCategory.ClinicalIbs),
        ("IBS-M", "Tipo de SII en el que se alternan la diarrea y el estreñimiento.", "Subtipo mixto del SII: más del 25 % de las deposiciones son tipo 1-2 de Bristol y más del 25 % tipo 6-7.", GlossaryCategory.ClinicalIbs),
        ("IBS-U", "Tipo de SII que no encaja claramente en diarrea, estreñimiento ni mixto.", "Subtipo no clasificable del SII: cumple criterios de SII pero el patrón de heces no encaja en IBS-D, IBS-C ni IBS-M.", GlossaryCategory.ClinicalIbs),
        ("IBS-SSS", "Es un cuestionario que mide qué tan fuertes son tus síntomas del SII, con un puntaje de 0 a 500. Se responde al inicio y cada dos semanas para ver si vas mejorando.", "Irritable Bowel Syndrome – Severity Scoring System: cuestionario validado de cinco dimensiones (0-500) que estratifica la severidad en leve (0-174), moderada (175-300) y severa (301-500). Métrica primaria del piloto; se aplica en la línea base y cada 14 días.", GlossaryCategory.ClinicalIbs),
        ("Distensión abdominal", "Sensación de hinchazón o de tener la barriga llena e inflada.", "Sensación subjetiva de aumento de presión abdominal, con o sin incremento objetivo del perímetro; síntoma cardinal en el SII.", GlossaryCategory.ClinicalIbs),
        ("Flatulencia", "Gases que se expulsan por el ano; es normal, pero en el SII puede aumentar.", "Expulsión de gas intestinal por vía rectal; su frecuencia puede incrementarse por fermentación de carbohidratos FODMAP.", GlossaryCategory.ClinicalIbs),
        ("Hábito intestinal", "La forma y frecuencia con que vas al baño a hacer popó.", "Patrón habitual de la defecación en frecuencia y consistencia; su cambio es criterio diagnóstico del SII.", GlossaryCategory.ClinicalIbs),
        ("Brote", "Momento en que los síntomas del SII empeoran de golpe por un tiempo.", "Exacerbación transitoria de los síntomas del SII, con frecuencia asociada a desencadenantes dietéticos o de estrés.", GlossaryCategory.ClinicalIbs),
        ("Ventana de 4 horas", "El tiempo después de comer en el que se relaciona un alimento con un síntoma.", "Ventana temporal canónica (4 h) para correlacionar comidas con síntomas, sustentada en la evidencia de Monash (2019) y Ford et al. (2024); se descarta la ventana de 24 h por inválida.", GlossaryCategory.ClinicalIbs),
        ("Línea base", "Es la primera medición de tus síntomas, con la que se compara tu avance.", "Evaluación IBS-SSS inicial (ciclo 0) que sirve de referencia para medir la respuesta clínica a lo largo del piloto.", GlossaryCategory.ClinicalIbs),

        // --- Nutricionales ---
        ("FODMAP", "Son ciertos azúcares y fibras de algunos alimentos que el intestino fermenta y pueden causar gases, hinchazón y molestias en personas con SII.", "Oligosacáridos, disacáridos, monosacáridos y polioles fermentables: carbohidratos de cadena corta mal absorbidos que aumentan la carga osmótica y la fermentación colónica, desencadenando síntomas en el SII.", GlossaryCategory.Nutritional),
        ("Oligosacáridos", "Un tipo de FODMAP presente en el trigo, la cebolla, el ajo y las legumbres.", "Fructanos y galacto-oligosacáridos (GOS): la 'O' de FODMAP; no se digieren por falta de enzimas y fermentan en el colon.", GlossaryCategory.Nutritional),
        ("Disacáridos", "Un tipo de FODMAP; el más común es la lactosa de la leche.", "Azúcares de dos unidades relevantes en FODMAP, principalmente la lactosa; su malabsorción depende de la actividad de la lactasa.", GlossaryCategory.Nutritional),
        ("Monosacáridos", "Un tipo de FODMAP; se refiere sobre todo a la fructosa de algunas frutas y la miel.", "Azúcares de una unidad relevantes en FODMAP, en especial la fructosa en exceso respecto de la glucosa.", GlossaryCategory.Nutritional),
        ("Polioles", "Un tipo de FODMAP presente en algunas frutas y en endulzantes 'sin azúcar'.", "Alcoholes de azúcar (sorbitol, manitol, xilitol, maltitol): la 'P' de FODMAP; se absorben de forma incompleta y ejercen efecto osmótico.", GlossaryCategory.Nutritional),
        ("Lactosa", "El azúcar de la leche y sus derivados. Algunas personas no la digieren bien.", "Disacárido de la leche cuya malabsorción, por déficit de lactasa, produce síntomas osmóticos y fermentativos.", GlossaryCategory.Nutritional),
        ("Fructosa", "Un azúcar natural de las frutas y la miel que a algunas personas les cae mal en exceso.", "Monosacárido cuya malabsorción, cuando está en exceso sobre la glucosa, contribuye a la sintomatología del SII.", GlossaryCategory.Nutritional),
        ("Fibra dietética", "La parte de los vegetales, frutas y cereales que no se digiere y ayuda al tránsito intestinal.", "Fracción no digerible de los alimentos vegetales; se distingue fibra soluble (fermentable, forma gel) e insoluble, con efectos distintos sobre el tránsito y los síntomas.", GlossaryCategory.Nutritional),
        ("Fase de eliminación", "Primera etapa de la dieta baja en FODMAP: se retiran los alimentos altos en FODMAP por unas semanas.", "Primera fase de la dieta baja en FODMAP (2-6 semanas): restricción estricta de alimentos altos en FODMAP para evaluar la respuesta sintomática.", GlossaryCategory.Nutritional),
        ("Fase de reintroducción", "Segunda etapa: se van probando los alimentos uno por uno para ver cuáles te caen bien.", "Segunda fase de la dieta baja en FODMAP: reintroducción sistemática y controlada de subgrupos FODMAP para identificar tolerancias individuales.", GlossaryCategory.Nutritional),
        ("Porción", "La cantidad de un alimento que comes de una vez.", "Cantidad de referencia de un alimento; en FODMAP la tolerancia depende de la dosis, por lo que la porción es clave.", GlossaryCategory.Nutritional),
        ("IMC", "Índice de Masa Corporal: un número que relaciona tu peso con tu estatura para saber si estás en un peso saludable.", "Índice de Masa Corporal (kg/m²): indicador antropométrico de peso relativo a la talla, categorizado según umbrales de la OMS.", GlossaryCategory.Nutritional),
        ("TPCA-CENAN", "Es la tabla oficial peruana con la información nutricional de los alimentos.", "Tablas Peruanas de Composición de Alimentos del CENAN-INS (2017); fuente primaria del catálogo de alimentos del sistema.", GlossaryCategory.Nutritional),

        // --- Sistema / proceso ---
        ("Recomendación", "Una sugerencia de alimentación que te da la app, revisada por tu nutricionista.", "Sugerencia dietética generada por el motor del sistema, sujeta a revisión humana (HITL) antes de entregarse al paciente; cada una es trazable a una versión de modelo.", GlossaryCategory.System),
        ("HITL", "Significa que un nutricionista de verdad revisa y aprueba cada recomendación antes de que la veas.", "Human-In-The-Loop: flujo en el que el nutricionista revisa, aprueba, modifica o rechaza las recomendaciones generadas por la IA antes de su entrega.", GlossaryCategory.System),
        ("Consentimiento informado", "El documento que aceptas para participar en el piloto, que explica cómo se usan tus datos.", "Documento firmado que autoriza el tratamiento de datos personales conforme a la Ley N° 29733; se registra con hash del texto para verificación posterior.", GlossaryCategory.System),
        ("Sincronización", "El proceso por el que lo que registras en tu celular sin internet se guarda luego en el sistema.", "Sincronización offline idempotente: los registros creados en el dispositivo (con client_guid propio) se concilian con el servidor cuando hay conexión, sin duplicados.", GlossaryCategory.System)
    };

    private readonly CauceDbContext _context;
    private readonly ILogger<GlossaryTermsSeeder> _logger;

    /// <summary>
    /// Inicializa el seeder con sus dependencias.
    /// </summary>
    /// <param name="context">Contexto de base de datos.</param>
    /// <param name="logger">Logger de la categoría del seeder.</param>
    public GlossaryTermsSeeder(CauceDbContext context, ILogger<GlossaryTermsSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Siembra el glosario de forma idempotente.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var utcNow = DateTime.UtcNow;
        var added = 0;

        foreach (var (term, patient, nutritionist, category) in Catalog)
        {
            var exists = await _context.GlossaryTerms
                .AnyAsync(x => x.Term == term, ct)
                .ConfigureAwait(false);

            if (!exists)
            {
                _context.GlossaryTerms.Add(
                    GlossaryTerm.SeedEntry(Guid.NewGuid(), term, patient, nutritionist, category, utcNow));
                added++;
            }
        }

        if (added > 0)
        {
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("Seeded {Count} glossary terms.", added);
        }
    }
}
