namespace Cauce.Domain.ClinicalRegistry.Enums;

/// <summary>
/// Nivel de carga FODMAP de un alimento (carbohidratos fermentables). Es el rasgo
/// principal del catálogo de alimentos para las recomendaciones dietéticas en SII.
/// En base de datos se persiste como <c>varchar</c> en snake_case lowercase
/// (<c>low</c>, <c>moderate</c>, <c>high</c>).
/// </summary>
public enum FodmapLevel
{
    /// <summary>
    /// Carga FODMAP baja: generalmente bien tolerada por pacientes con SII.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Carga FODMAP moderada: tolerable en porciones controladas.
    /// </summary>
    Moderate = 1,

    /// <summary>
    /// Carga FODMAP alta: candidata a desencadenar síntomas.
    /// </summary>
    High = 2
}
