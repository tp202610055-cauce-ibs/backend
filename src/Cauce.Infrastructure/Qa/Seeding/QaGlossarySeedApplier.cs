using Cauce.Domain.ClinicalRegistry;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Qa.Manifest;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Qa.Seeding;

/// <summary>
/// Términos del glosario que piden los casos. No pertenecen a ninguna cuenta: se alinean una vez por ejecución.
/// Un término presente recupera la categoría del caso; uno que falta o sobra no se crea ni se borra, lo informa verify-qa.
/// </summary>
public sealed class QaGlossarySeedApplier : IQaSeedApplier
{
    /// <inheritdoc />
    public int Order => 0;

    /// <inheritdoc />
    public bool CanApply(QaSeed seed) => seed is QaGlossarySeed;

    /// <inheritdoc />
    public Task ApplyAsync(QaSeed seed, QaSeedContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        return AlignAsync(context.Db, (QaGlossarySeed)seed, ct);
    }

    /// <summary>
    /// Deja el término en la categoría del caso, sin guardar. El agregado no expone cómo cambiarla: se escribe la
    /// propiedad directamente, solo desde la siembra de QA.
    /// </summary>
    public static async Task AlignAsync(CauceDbContext db, QaGlossarySeed seed, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(seed);
        if (!seed.Present || seed.Category is null || !QaManifestValidator.TryParseGlossaryCategory(seed.Category, out var category))
        {
            return;
        }

        var term = await db.GlossaryTerms.FirstOrDefaultAsync(candidate => candidate.Term == seed.Term, ct).ConfigureAwait(false);
        if (term is not null && term.Category != category)
        {
            db.Entry(term).Property(nameof(GlossaryTerm.Category)).CurrentValue = category;
        }
    }
}
