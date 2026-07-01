using System.ComponentModel.DataAnnotations;

namespace Cauce.Application.Recommendations.Configuration;

/// <summary>
/// Opciones de conexión con el modelo de lenguaje local (Ollama) para la generación de
/// explicaciones.
/// </summary>
public sealed class OllamaOptions
{
    /// <summary>
    /// URL del endpoint de generación de Ollama.
    /// </summary>
    public string Endpoint { get; init; } = "http://ollama:11434/api/generate";

    /// <summary>
    /// Nombre del modelo a invocar.
    /// </summary>
    public string Model { get; init; } = "llama3.1:8b-instruct-q4_K_M";

    /// <summary>
    /// Tiempo máximo de espera de la generación, en segundos.
    /// </summary>
    [Range(1, 120)]
    public int TimeoutSeconds { get; init; } = 15;

    /// <summary>
    /// Longitud máxima permitida de la explicación generada, en caracteres.
    /// </summary>
    [Range(1, 2000)]
    public int MaxOutputCharacters { get; init; } = 500;

    /// <summary>
    /// Longitud mínima requerida de la explicación generada, en caracteres.
    /// </summary>
    [Range(1, 2000)]
    public int MinOutputCharacters { get; init; } = 80;
}
