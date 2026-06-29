using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Exceptions;
using FluentAssertions;

namespace Cauce.Domain.Tests.ClinicalRegistry;

/// <summary>
/// Pruebas de invariantes y métodos de dominio de <see cref="ClinicalNote"/>.
/// </summary>
public sealed class ClinicalNoteTests
{
    private static readonly DateTime Now = new(2026, 6, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PatientId = Guid.NewGuid();

    [Fact]
    public void Attach_WithMealId_CreatesNote()
    {
        var note = ClinicalNote.Attach(Guid.NewGuid(), PatientId, Guid.NewGuid(), null, "Dolor leve", Now);

        note.MealId.Should().NotBeNull();
        note.SymptomId.Should().BeNull();
    }

    [Fact]
    public void Attach_WithSymptomId_CreatesNote()
    {
        var note = ClinicalNote.Attach(Guid.NewGuid(), PatientId, null, Guid.NewGuid(), "Comentario", Now);

        note.SymptomId.Should().NotBeNull();
        note.MealId.Should().BeNull();
    }

    [Fact]
    public void Attach_WithBothAssociations_Throws()
    {
        var act = () => ClinicalNote.Attach(Guid.NewGuid(), PatientId, Guid.NewGuid(), Guid.NewGuid(), "x", Now);

        act.Should().Throw<InvalidClinicalNoteAssociationException>();
    }

    [Fact]
    public void Attach_WithNoAssociation_Throws()
    {
        var act = () => ClinicalNote.Attach(Guid.NewGuid(), PatientId, null, null, "x", Now);

        act.Should().Throw<InvalidClinicalNoteAssociationException>();
    }

    [Fact]
    public void Attach_EmptyContent_Throws()
    {
        var act = () => ClinicalNote.Attach(Guid.NewGuid(), PatientId, Guid.NewGuid(), null, "  ", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Attach_ContentTooLong_Throws()
    {
        var content = new string('a', 501);
        var act = () => ClinicalNote.Attach(Guid.NewGuid(), PatientId, Guid.NewGuid(), null, content, Now);

        act.Should().Throw<ArgumentException>();
    }
}
