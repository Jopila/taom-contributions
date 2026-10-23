using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;

namespace TAOM.Features.ServerSettings;

/// <summary>
/// The server settings file's boot step (ADR-013 decision 2). At ProcessLoad on a dedicated server it forces the
/// load (if no earlier settings read did), writes the template when no file exists and the defaults reference
/// when it is stale, and logs the boot line and the per-key lines. On a client it does nothing.
/// Not player-facing, so no localization.
/// </summary>
internal sealed class ServerSettingsModule : TaomFeatureModule
{
    public override string Id => "ServerSettings";

    public override void OnPhase(ApplyPhase phase, IResolver resolver)
    {
        if (phase != ApplyPhase.ProcessLoad) return;

        _ = ServerSettingsSource.Get<TaomSettings>();
        var report = ServerSettingsSource.Report;
        if (report == null) return;

        var logger = resolver.Resolve<IModLogger>();
        logger.LogInfo(ServerSettingsReport.Tag + report.BootLine());
        report.WriteTo(logger);

        var location = report.Location;
        if (location == null) return;
        var store = ServerSettingsSource.Store;
        if (report.State == ServerFileState.Missing)
        {
            var templateError = store.WriteIfMissing(location.Path, ServerSettingsDefaultsRenderer.Template());
            if (templateError == null)
                logger.LogInfo(ServerSettingsReport.Tag + "wrote an empty template to " + location.Path);
            else
                logger.LogWarning(ServerSettingsReport.Tag + templateError);
        }

        var referenceError = store.WriteIfChanged(location.DefaultsPath,
            ServerSettingsDefaultsRenderer.DefaultsReference(new ServerSettingsSet()));
        if (referenceError != null)
            logger.LogWarning(ServerSettingsReport.Tag + referenceError);
    }
}
