using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cauce.Infrastructure.Outbox;

/// <summary>
/// Opciones de serialización JSON compartidas para la carga de los mensajes de outbox. Los enums se
/// serializan como texto para que la columna <c>payload_json</c> sea legible y estable.
/// </summary>
internal static class OutboxSerialization
{
    /// <summary>
    /// Opciones de serialización usadas al publicar y al despachar mensajes de outbox.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
