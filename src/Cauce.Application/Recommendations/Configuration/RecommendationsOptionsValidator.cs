using Microsoft.Extensions.Options;

namespace Cauce.Application.Recommendations.Configuration;

/// <summary>
/// Validador de <see cref="RecommendationsOptions"/> que verifica, al arranque, los rangos y
/// las invariantes cruzadas que las anotaciones de datos no cubren. Una configuración inválida
/// impide el inicio de la aplicación.
/// </summary>
public sealed class RecommendationsOptionsValidator : IValidateOptions<RecommendationsOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, RecommendationsOptions options)
    {
        var failures = new List<string>();

        if (options.EngineKind is not ("Rule" or "Onnx"))
        {
            failures.Add($"Recommendations:EngineKind debe ser 'Rule' u 'Onnx'. Se recibió '{options.EngineKind}'.");
        }

        if (options.CandidateFoodsWindowDays is < 1 or > 60)
        {
            failures.Add("Recommendations:CandidateFoodsWindowDays debe estar entre 1 y 60.");
        }

        if (options.MinCandidateFoodsCount is < 1 or > 20)
        {
            failures.Add("Recommendations:MinCandidateFoodsCount debe estar entre 1 y 20.");
        }

        if (options.AutoApprovalThreshold is < 0m or > 1m)
        {
            failures.Add("Recommendations:AutoApprovalThreshold debe estar entre 0 y 1.");
        }

        if (options.MaxAvoidItemsForAutoApproval < 0)
        {
            failures.Add("Recommendations:MaxAvoidItemsForAutoApproval no puede ser negativo.");
        }

        if (options.ExpirationWindowHours is < 1 or > 720)
        {
            failures.Add("Recommendations:ExpirationWindowHours debe estar entre 1 y 720.");
        }

        if (options.AvoidThreshold is < 0m or > 1m)
        {
            failures.Add("Recommendations:AvoidThreshold debe estar entre 0 y 1.");
        }

        if (options.ReduceThreshold is < 0m or > 1m)
        {
            failures.Add("Recommendations:ReduceThreshold debe estar entre 0 y 1.");
        }

        if (options.AvoidThreshold <= options.ReduceThreshold)
        {
            failures.Add("Recommendations:AvoidThreshold debe ser mayor que Recommendations:ReduceThreshold.");
        }

        if (options.Ollama.MinOutputCharacters > options.Ollama.MaxOutputCharacters)
        {
            failures.Add("Recommendations:Ollama:MinOutputCharacters no puede superar a MaxOutputCharacters.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
