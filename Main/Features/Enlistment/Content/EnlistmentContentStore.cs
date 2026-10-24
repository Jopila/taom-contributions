using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

public class EnlistmentContentStore : IEnlistmentContentStore
{
    private const string HistoryKey = "history=";

    private readonly ServiceContentRecord _record = new ServiceContentRecord();
    private ServiceHistory _history = new ServiceHistory();
    private readonly IModLogger _logger;

    public EnlistmentContentStore(IModLogger logger)
    {
        _logger = logger;
    }

    public ServiceContentRecord Record => _record;

    public ServiceHistory History => _history;

    public string Serialize()
    {
        var section = _record.Serialize();
        var history = _history.Serialize();
        return history.Length == 0 ? section : section + ";" + HistoryKey + history;
    }

    public void Deserialize(string section)
    {
        // The history is read on its own: a malformed progression record must not cost the past.
        _history = ParseHistory(section);

        if (string.IsNullOrEmpty(section))
        {
            _record.Reset();
            return;
        }

        if (ServiceContentRecord.TryParse(section, out var parsed))
        {
            _record.CopyFrom(parsed);
            return;
        }

        _logger?.LogWarning("EnlistmentContentStore: malformed content section — resetting progression (core service state is unaffected)");
        _record.Reset();
    }

    /// <summary>The new-campaign reset: progress and the past both go. A discharge resets only <see cref="Record"/>.</summary>
    public void Clear()
    {
        _record.Reset();
        _history = new ServiceHistory();
    }

    private static ServiceHistory ParseHistory(string section)
    {
        if (string.IsNullOrEmpty(section))
            return new ServiceHistory();
        foreach (var part in section.Split(';'))
        {
            if (part.StartsWith(HistoryKey, System.StringComparison.Ordinal))
                return ServiceHistory.Parse(part.Substring(HistoryKey.Length));
        }
        return new ServiceHistory();
    }
}
