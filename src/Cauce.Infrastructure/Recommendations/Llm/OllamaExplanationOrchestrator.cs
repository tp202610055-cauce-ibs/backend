using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.Configuration;
using Cauce.Application.Recommendations.Contracts;
using Cauce.Domain.Recommendations.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Recommendations.Llm;

/// <summary>
/// Orquestador de explicaciones respaldado por Ollama (Llama 3.1 8B Q4, auto-hospedado). Invoca al
/// modelo dentro de un tiempo límite y valida su salida con los guardrails clínicos; ante timeout,
/// error o salida no conforme, cae a la plantilla de respaldo (DEC-B4-07). Cumple la restricción de
/// que ningún dato clínico salga del servidor (Ley 29733).
/// </summary>
public sealed class OllamaExplanationOrchestrator : IExplanationOrchestrator
{
    private readonly HttpClient _http;
    private readonly RecommendationsOptions _options;
    private readonly RecommendationGuardrailsValidator _guardrails;
    private readonly FallbackExplanationProvider _fallback;
    private readonly ILogger<OllamaExplanationOrchestrator> _logger;

    /// <summary>
    /// Inicializa el orquestador con sus dependencias.
    /// </summary>
    public OllamaExplanationOrchestrator(
        HttpClient http,
        IOptions<RecommendationsOptions> options,
        RecommendationGuardrailsValidator guardrails,
        FallbackExplanationProvider fallback,
        ILogger<OllamaExplanationOrchestrator> logger)
    {
        _http = http;
        _options = options.Value;
        _guardrails = guardrails;
        _fallback = fallback;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ExplanationResult> ExplainAsync(
        PatientContextSnapshot patient,
        IReadOnlyList<ExplanationItem> items,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(_options.Ollama.TimeoutSeconds));

            var prompt = RecommendationPromptTemplate.Build(patient, items);
            var requestBody = new OllamaRequest(
                _options.Ollama.Model,
                prompt,
                Stream: false,
                new OllamaRequestOptions(0.3, 200));

            var response = await _http
                .PostAsJsonAsync(_options.Ollama.Endpoint, requestBody, timeoutSource.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Ollama returned {StatusCode}; using fallback explanation.", response.StatusCode);
                return _fallback.GenerateFor(items);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<OllamaResponse>(timeoutSource.Token)
                .ConfigureAwait(false);
            var text = payload?.Response?.Trim() ?? string.Empty;

            var validation = _guardrails.Validate(text);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Ollama output failed guardrails ({Reason}); using fallback explanation.", validation.FailureReason);
                return _fallback.GenerateFor(items);
            }

            return new ExplanationResult(text, ExplanationSource.LlmGenerated);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Ollama call timed out; using fallback explanation.");
            return _fallback.GenerateFor(items);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Ollama call failed; using fallback explanation.");
            return _fallback.GenerateFor(items);
        }
    }

    private sealed record OllamaRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("options")] OllamaRequestOptions Options);

    private sealed record OllamaRequestOptions(
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("num_predict")] int NumPredict);

    private sealed record OllamaResponse(
        [property: JsonPropertyName("response")] string? Response);
}
