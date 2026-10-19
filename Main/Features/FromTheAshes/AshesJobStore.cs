using System;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.FromTheAshes;

/// <summary>
/// The one job the main party may have, and its save halves. The record travels as primitives (the behavior's SyncData
/// keys), so a corrupt value is testable without a campaign. A bad record is dropped with one warning and never throws:
/// a throw out of a load handler loops the loading screen (docs/reviews/lessons/campaign-mechanics.md).
/// </summary>
public sealed class AshesJobStore
{
    private readonly IModLogger _logger;

    public AshesJobStore(IModLogger logger) => _logger = logger;

    public AshesJob? Current { get; private set; }

    public void Set(AshesJob job) => Current = job;

    public void Clear() => Current = null;

    /// <summary>Copies the live job into the save locals; an empty store writes an empty record. Never changes the store.</summary>
    public void SaveInto(out string settlementId, out int kind, out double startHours, out float totalHours, out int hoursCredited)
    {
        var job = Current;
        settlementId = job?.SettlementId ?? "";
        kind = job == null ? 0 : (int)job.Kind;
        startHours = job?.StartHours ?? 0d;
        totalHours = job?.TotalHours ?? 0f;
        hoursCredited = job?.HoursCredited ?? 0;
    }

    /// <summary>
    /// Replaces the live job with the saved record (a load always replaces: the store is process-lifetime, and the previous
    /// campaign's job must not leak into this one). An empty record is "no job". A record that cannot be a job (unknown
    /// kind, a start or length that is not finite, a length of zero or less or beyond <see cref="AshesJob.MaxHours"/>, hours credited outside 0 to the length
    /// rounded up) is dropped with one warning.
    /// </summary>
    public void LoadFrom(string? settlementId, int kind, double startHours, float totalHours, int hoursCredited)
    {
        Current = null;
        if (string.IsNullOrEmpty(settlementId))
        {
            if (kind != 0 || totalHours != 0f || hoursCredited != 0)
                Drop("", "an empty settlement id");
            return;
        }

        if (kind != (int)AshesJobKind.VillageRebuild && kind != (int)AshesJobKind.ProjectHelp)
            Drop(settlementId!, $"unknown kind {kind}");
        else if (!FiniteFloatValidator.IsFinite(startHours))
            Drop(settlementId!, "a start time that is not finite");
        else if (!FiniteFloatValidator.IsFinite(totalHours) || totalHours <= 0f || totalHours > AshesJob.MaxHours)
            Drop(settlementId!, $"a length of {totalHours} hours");
        else if (hoursCredited < 0 || hoursCredited > (int)Math.Ceiling(totalHours))
            Drop(settlementId!, $"{hoursCredited} hours credited of {totalHours}");
        else
            Current = new AshesJob
            {
                Kind = (AshesJobKind)kind,
                SettlementId = settlementId!,
                StartHours = startHours,
                TotalHours = totalHours,
                HoursCredited = hoursCredited,
            };
    }

    private void Drop(string settlementId, string why) =>
        _logger.LogWarning($"[FromTheAshes] dropped a saved job at '{settlementId}': {why}");
}
