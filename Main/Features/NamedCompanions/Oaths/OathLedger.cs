using System.Collections.Generic;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.NamedCompanions.Oaths;

/// <summary>The saved shape of <see cref="OathLedger"/>. Any member may be null (older or hand-edited saves).</summary>
public sealed class OathLedgerSaveData
{
    public List<string> Flags { get; set; }

    /// <summary>The day each topic was last used, keyed <c>character_id|topic_id</c>.</summary>
    public Dictionary<string, int> TopicDays { get; set; }
}

/// <summary>
/// Per-campaign oath state: global flags and the day each topic was last used (a topic with a recorded day is done).
/// A process singleton, so the campaign behavior calls <see cref="ResetForNewSession"/> before any
/// SyncData (csharp-architecture.md, "Singleton Services Holding Per-Campaign State").
/// </summary>
public sealed class OathLedger : IOathFlagStore
{
    private readonly ICoopSessionProvider _coop;
    private readonly HashSet<string> _flags = new();
    private readonly Dictionary<string, int> _topicDays = new();

    public OathLedger(ICoopSessionProvider coop)
    {
        _coop = coop;
    }

    public bool Has(string flag) => OathFlagName.TryNormalize(flag, out var name) && _flags.Contains(name);

    public bool Set(string flag)
    {
        if (!MayWrite() || !OathFlagName.TryNormalize(flag, out var name)) return false;
        _flags.Add(name);
        return true;
    }

    public bool TryGetTopicLastDay(string characterId, string topicId, out int day) =>
        _topicDays.TryGetValue(Key(characterId, topicId), out day);

    public bool SetTopicLastDay(string characterId, string topicId, int day)
    {
        if (!MayWrite()) return false;
        _topicDays[Key(characterId, topicId)] = day;
        return true;
    }

    public void ResetForNewSession()
    {
        _flags.Clear();
        _topicDays.Clear();
    }

    public OathLedgerSaveData Export() => new()
    {
        Flags = new List<string>(_flags),
        TopicDays = new Dictionary<string, int>(_topicDays),
    };

    /// <summary>Replaces the current state with the saved one. Loading is not a gameplay write, so it ignores the co-op gate.</summary>
    public void Import(OathLedgerSaveData data)
    {
        ResetForNewSession();
        if (data == null) return;

        if (data.Flags != null)
            foreach (var flag in data.Flags)
                if (OathFlagName.TryNormalize(flag, out var name)) _flags.Add(name);

        if (data.TopicDays != null)
            foreach (var pair in data.TopicDays)
                if (!string.IsNullOrEmpty(pair.Key)) _topicDays[pair.Key] = pair.Value;
    }

    private bool MayWrite() => CoopSessionPolicy.MayWriteSaveBackedState(_coop.IsCoopClient);

    private static string Key(string characterId, string topicId) => characterId + "|" + topicId;
}
