using Cauce.Domain.ClinicalRegistry;
using FluentAssertions;

namespace Cauce.Domain.Tests.ClinicalRegistry;

/// <summary>
/// Pruebas del <c>client_guid</c> de la nota clínica, la clave de idempotencia que el dispositivo
/// genera (DEC-B3-04).
/// </summary>
public sealed class ClinicalNoteClientGuidTests
{
    private static readonly DateTime Now = new(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Attach_WithClientGuid_PersistsIt()
    {
        var clientGuid = Guid.NewGuid();

        var note = ClinicalNote.Attach(Guid.NewGuid(), clientGuid, Guid.NewGuid(), Guid.NewGuid(), null, "Dolor leve", Now);

        note.ClientGuid.Should().Be(clientGuid);
    }

    [Fact]
    public void Attach_EmptyClientGuid_Throws()
    {
        var act = () => ClinicalNote.Attach(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), null, "Dolor leve", Now);

        act.Should().Throw<ArgumentException>();
    }
}
