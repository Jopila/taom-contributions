using System.Collections.Generic;
using TAOM.Features.NamedCompanions.Domain;

namespace TAOM.Features.NamedCompanions.Oaths;

public enum HireStatus
{
    /// <summary>The companion has no custom terms (or is disabled): vanilla hiring applies.</summary>
    None,

    /// <summary>A required flag is not set yet.</summary>
    Locked,

    /// <summary>Gold or items are short; <see cref="HireEvaluation.Missing"/> lists them.</summary>
    Missing,

    /// <summary>Every term is met.</summary>
    Met,
}

public enum MissingTermKind
{
    Gold,
    Item,
}

/// <summary>One unmet hire term, reported by the hire service.</summary>
public sealed class MissingTerm
{
    public MissingTermKind Kind { get; set; }
    public string ItemId { get; set; }
    public int Count { get; set; }
}

public sealed class HireEvaluation
{
    /// <summary>A fresh instance per call: the type is mutable, so a shared one could be changed under every reader.</summary>
    public static HireEvaluation NoTerms => new() { Status = HireStatus.None };

    public HireStatus Status { get; set; }

    /// <summary>The gold the companion asks for: the configured gold, else the vanilla hiring price.</summary>
    public int Price { get; set; }

    /// <summary>The terms this evaluation read; null when <see cref="Status"/> is <see cref="HireStatus.None"/>.</summary>
    public HireTermsDefinition Terms { get; set; }

    public List<MissingTerm> Missing { get; } = new();
}
