using Cauce.Infrastructure.Qa.Manifest;

namespace Cauce.Infrastructure.Qa.Seeding;

/// <summary>
/// Notas y estados de cuenta: no siembran datos. El estado de la cuenta lo aplica el aprovisionador.
/// </summary>
public sealed class QaNoDataSeedApplier : IQaSeedApplier
{
    /// <inheritdoc />
    public int Order => 0;

    /// <inheritdoc />
    public bool CanApply(QaSeed seed) => seed is QaNoteSeed or QaAccountSeed;

    /// <inheritdoc />
    public Task ApplyAsync(QaSeed seed, QaSeedContext context, CancellationToken ct) => Task.CompletedTask;
}
