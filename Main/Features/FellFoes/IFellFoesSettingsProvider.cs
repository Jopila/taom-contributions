using TAOM.Features.FellFoes.Domain;

namespace TAOM.Features.FellFoes;

/// <summary>The MCM values behind Fell Foes. Numbers arrive already validated as <see cref="FellFoesTunables"/>.</summary>
public interface IFellFoesSettingsProvider
{
    bool CorneredRoutersEnabled { get; }

    bool ArchersTargetPlayerEnabled { get; }

    bool StragglersLeaveFieldEnabled { get; }

    /// <summary>Writes a <c>[FellFoes]</c> line each second something is held or done.</summary>
    bool DebugEnabled { get; }

    /// <summary>Validated numbers; rebuilt on each read so an MCM edit applies within a second.</summary>
    FellFoesTunables Tunables { get; }
}
