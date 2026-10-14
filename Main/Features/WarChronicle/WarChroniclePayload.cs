using System.Collections.Generic;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Events;

namespace TAOM.Features.WarChronicle;

/// <summary>
/// The War Chronicle's save content, section by section, between the services and the JSON
/// (<see cref="WarChronicleSaveCodec"/>). Each section is read on its own: a section that fails to
/// parse comes back empty with a line in <see cref="Warnings"/>, the others unaffected.
/// </summary>
internal sealed class WarChroniclePayload
{
    public List<WarEffect> Effects { get; } = new List<WarEffect>();

    /// <summary>Empty when the save had no baselines section, or one that did not parse.</summary>
    public Dictionary<string, int> Baselines { get; } = new Dictionary<string, int>();

    public Dictionary<string, int> RallyTiers { get; } = new Dictionary<string, int>();

    /// <summary>The chronicle's event rows (#765 M3), owned by <see cref="ChronicleStateStore"/>.</summary>
    public List<ChronicleEventRecord> Chronicle { get; } = new List<ChronicleEventRecord>();

    /// <summary>
    /// The day the chronicle first saw each anchoring phase reached (decided 2026-10-14), owned by
    /// <see cref="ChronicleStateStore"/>; empty for a save without them.
    /// </summary>
    public Dictionary<WarPhase, double> PhaseSeenDays { get; } = new Dictionary<WarPhase, double>();

    /// <summary>What was skipped or reset while parsing, one line each, for the log.</summary>
    public List<string> Warnings { get; } = new List<string>();
}
