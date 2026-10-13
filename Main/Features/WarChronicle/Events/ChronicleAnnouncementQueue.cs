using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.WarChronicle.Events;

/// <summary>
/// The chronicle's announcements (docs/features/war-chronicle.md "Announcements"): an in-memory queue,
/// not saved, drained one item per campaign tick and only when the map is free.
///
/// <list type="bullet">
/// <item><b>No stacking:</b> one inquiry at a time, released by its close callback (the CareerQuest
/// <c>_offerPending</c> guard). A show that throws releases the latch and drops the item, so the queue
/// never sticks.</item>
/// <item><b>Map free:</b> <see cref="IChroniclePresenter.IsMapFree"/>, the Messenger
/// <c>IsPlayerAvailable</c> check.</item>
/// <item><b>Authority re-checked at show time:</b> only the co-op authority runs the chronicle, so only
/// it announces (clients see nothing, as with the momentum meter).</item>
/// <item><b>Per campaign:</b> <see cref="ResetForNewSession"/> runs from the events behavior's
/// constructor; a close callback from an earlier campaign's popup cannot release the new latch.</item>
/// </list>
/// The empty-queue test comes first, because the campaign tick calls this every frame.
/// </summary>
public sealed class ChronicleAnnouncementQueue
{
    /// <summary>Oldest items drop past this; a burst (a save past many event days) never piles up popups.</summary>
    public const int MaxQueued = 16;

    private readonly IChroniclePresenter _presenter;
    private readonly ICoopSessionProvider _coop;
    private readonly IWarChronicleSettingsProvider _settings;
    private readonly IModLogger _logger;
    private readonly Queue<ChronicleAnnouncement> _queue = new Queue<ChronicleAnnouncement>();

    private bool _showing;
    private int _session;

    public ChronicleAnnouncementQueue(
        IChroniclePresenter presenter, ICoopSessionProvider coop, IWarChronicleSettingsProvider settings, IModLogger logger)
    {
        _presenter = presenter;
        _coop = coop;
        _settings = settings;
        _logger = logger;
    }

    public int Count => _queue.Count;

    /// <summary>Queues the announcement unless Show Announcements is off.</summary>
    public void Enqueue(ChronicleAnnouncement announcement)
    {
        if (announcement == null || !_settings.ChronicleAnnouncements)
            return;

        _queue.Enqueue(announcement);
        while (_queue.Count > MaxQueued)
            _queue.Dequeue();
    }

    /// <summary>Shows the oldest item when nothing is open, this peer is the authority and the map is free.</summary>
    public void TryShowNext()
    {
        if (_queue.Count == 0 || _showing)
            return;
        if (!_coop.IsAuthority || !_presenter.IsMapFree())
            return;

        var next = _queue.Dequeue();
        var session = _session;
        _showing = true;
        try
        {
            _presenter.Show(next, () =>
            {
                if (session == _session)
                    _showing = false;
            });
        }
        catch (Exception ex)
        {
            _showing = false;
            _logger.LogWarning($"[WarChronicle] Announcement for '{next.EventId}' could not be shown ({ex.GetType().Name}: {ex.Message}).");
        }
    }

    /// <summary>Empties the queue and releases the latch: called from the events behavior's constructor.</summary>
    public void ResetForNewSession()
    {
        _queue.Clear();
        _showing = false;
        _session++;
    }
}
