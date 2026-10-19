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
/// <item><b>No stacking:</b> <see cref="IChroniclePresenter.IsMapFree"/> is false while any inquiry is open
/// (<c>InformationManager.IsAnyInquiryActive</c>), ours included, so the queue keeps no latch of its own and
/// a popup closed without its button action (<c>InformationManager.HideInquiry</c>) cannot stall it. A show
/// that throws drops the item.</item>
/// <item><b>Switches re-checked at show time:</b> with the chronicle or its announcements switched off, what
/// is still queued is dropped unshown.</item>
/// <item><b>Map free, then authority:</b> the map test (<c>MapPopupGate.IsMapFree</c>) runs
/// before the co-op probe, which uses reflection. Only the co-op authority runs the chronicle, so only it
/// announces (clients see nothing, as with the momentum meter).</item>
/// <item><b>Per campaign:</b> <see cref="ResetForNewSession"/> runs from the events behavior's constructor.</item>
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

    /// <summary>Shows the oldest item when the map is free (no inquiry open) and this peer is the authority.</summary>
    public void TryShowNext()
    {
        if (_queue.Count == 0)
            return;
        if (!_settings.ChronicleEnabled || !_settings.ChronicleAnnouncements)
        {
            _queue.Clear();
            return;
        }

        if (!_presenter.IsMapFree() || !_coop.IsAuthority)
            return;

        var next = _queue.Dequeue();
        try
        {
            _presenter.Show(next);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[WarChronicle] Announcement for '{next.EventId}' could not be shown ({ex.GetType().Name}: {ex.Message}).");
        }
    }

    /// <summary>Empties the queue: called from the events behavior's constructor.</summary>
    public void ResetForNewSession() => _queue.Clear();
}
