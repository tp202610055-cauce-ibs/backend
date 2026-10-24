using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Cauce.Application.Recommendations.Configuration;
using Cauce.Application.Recommendations.Contracts;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Infrastructure.Recommendations.Llm;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Tests.Recommendations;

/// <summary>
/// Pruebas del orquestador de Ollama cuando el servicio no existe, como en la VM de QA: cae al texto de respaldo sin
/// lanzar y antes del timeout.
/// </summary>
public sealed class OllamaExplanationOrchestratorTests
{
    private const int TimeoutSeconds = 15;

    public static TheoryData<string> UnreachableEndpoints => new()
    {
        $"http://127.0.0.1:{ClosedPort()}/api/generate",
        "http://ollama.invalid:11434/api/generate"
    };

    [Theory]
    [MemberData(nameof(UnreachableEndpoints))]
    public async Task ExplainAsync_UnreachableEndpoint_ReturnsFallbackWithinTheTimeout(string endpoint)
    {
        var options = Options.Create(new RecommendationsOptions { Ollama = new OllamaOptions { Endpoint = endpoint, TimeoutSeconds = TimeoutSeconds } });
        using var http = new HttpClient();
        var orchestrator = new OllamaExplanationOrchestrator(
            http, options, new RecommendationGuardrailsValidator(options), new FallbackExplanationProvider(), NullLogger<OllamaExplanationOrchestrator>.Instance);
        var stopwatch = Stopwatch.StartNew();

        var result = await orchestrator.ExplainAsync(
            new PatientContextSnapshot(30, "Femenino", 22m, "SII-D", 1, "Media", false, false),
            [new ExplanationItem("Cebolla cocida", ActionType.Reduce, null)],
            CancellationToken.None);

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(TimeoutSeconds));
        result.Source.Should().Be(ExplanationSource.Fallback);
        result.Text.Should().NotBeNullOrWhiteSpace();
        result.FallbackReason.Should().Be("error");
        result.FallbackErrorType.Should().Be(nameof(HttpRequestException));
    }

    // Un puerto que el sistema acaba de liberar: nadie escucha en él y la conexión se rechaza.
    private static int ClosedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
