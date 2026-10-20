using System;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;

namespace TAOM.Features.LordFaceVariety;

/// <summary>
/// Entry point for Lord Face Variety (#952): runs the face pass once, when a NEW campaign is created.
/// It listens to <c>OnNewGameCreatedPartialFollowUpEndEvent</c>, which fires once after follow-ups 0..99
/// (<c>CampaignEvents.OnNewGameCreated</c>), so heroes made by other behaviors' follow-ups exist by then. It
/// never listens to a load event: existing saves keep the faces they have (the maintainer's decision). The pass writes <c>Hero.StaticBodyProperties</c>, which
/// the save already stores, so the behavior holds no state and saves nothing. A failure is logged and the
/// campaign starts anyway.
/// </summary>
public sealed class LordFaceVarietyBehavior : CampaignBehaviorBase
{
    private readonly LordFaceVarietyService _service;
    private readonly IModLogger _logger;

    public LordFaceVarietyBehavior(LordFaceVarietyService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public override void RegisterEvents() =>
        CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener(this, _ => Vary());

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void Vary()
    {
        try
        {
            _service.Run();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[LordFaceVariety] pass failed, faces left as they are: {ex}");
        }
    }
}
