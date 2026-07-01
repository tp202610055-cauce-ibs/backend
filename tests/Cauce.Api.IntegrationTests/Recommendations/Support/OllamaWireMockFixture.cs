using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Cauce.Api.IntegrationTests.Recommendations.Support;

/// <summary>
/// Doble de Ollama basado en WireMock.Net para las pruebas de integración del orquestador LLM
/// (Sección 8.4). Permite simular respuestas exitosas, fallos y timeouts.
/// </summary>
public sealed class OllamaWireMockFixture : IDisposable
{
    private readonly WireMockServer _server;

    /// <summary>
    /// Inicia el servidor simulado.
    /// </summary>
    public OllamaWireMockFixture()
    {
        _server = WireMockServer.Start();
    }

    /// <summary>
    /// Endpoint de generación que consume la aplicación bajo prueba.
    /// </summary>
    public string Endpoint => $"{_server.Url}/api/generate";

    /// <summary>
    /// Reinicia todas las reglas configuradas.
    /// </summary>
    public void Reset() => _server.Reset();

    /// <summary>
    /// Configura una respuesta exitosa con el texto indicado.
    /// </summary>
    /// <param name="responseText">Texto de la explicación devuelta.</param>
    public void StubSuccess(string responseText)
    {
        _server
            .Given(Request.Create().WithPath("/api/generate").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new { response = responseText }));
    }

    /// <summary>
    /// Configura una respuesta de error con el código indicado.
    /// </summary>
    /// <param name="statusCode">Código de estado HTTP a devolver.</param>
    public void StubFailure(int statusCode = 500)
    {
        _server
            .Given(Request.Create().WithPath("/api/generate").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(statusCode));
    }

    /// <summary>
    /// Configura una respuesta que excede el tiempo de espera.
    /// </summary>
    public void StubTimeout()
    {
        _server
            .Given(Request.Create().WithPath("/api/generate").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithDelay(TimeSpan.FromSeconds(30))
                .WithBodyAsJson(new { response = "respuesta tardía" }));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
    }
}
