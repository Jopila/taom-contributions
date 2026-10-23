using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bannerlord.UIExtenderEx;
using TAOM.Core.Logging;
using TAOM.Dependencies.Foundation;
using TAOM.Features.CoopInterop;

namespace TAOM.Client;

/// <summary>
/// Registers TAOM's UIExtenderEx extensions from the client submodule (ADR-013). Every extension lives in
/// TAOM.Client, and UIExtenderEx reads one assembly per Register call and refuses a second call under one module
/// name, so this is the only registration.
/// </summary>
internal static class ClientUiExtensions
{
    /// <summary>
    /// #370 — registers TAOM's UIExtenderEx extensions, minus any marked
    /// <c>[CoopSuppressedUi]</c> when a co-op module is active.
    ///
    /// Mirrors UIExtenderEx's own <c>Register(Assembly)</c> type selection (any type carrying an
    /// attribute derived from <c>BaseUIExtenderAttribute</c>), then filters. Registration is a
    /// one-shot here in <c>OnSubModuleLoad</c>, so this is the only point at which a widget can be
    /// kept out of the prefab — a runtime check inside a mixin cannot un-inject one.
    ///
    /// Never throws: a failure here would cost every TAOM UI extension, so the fallback is the
    /// unfiltered assembly registration (solo behaviour).
    /// </summary>
    internal static void Register(UIExtender extender, Assembly assembly) =>
        Register(assembly, () => CoopPresence.IsActive, IoC.Resolve<IModLogger>(),
            extender.Register, types => extender.Register(types));

    /// <summary>
    /// The registration logic with its three inputs passed in, so a test can drive the solo, co-op and
    /// probe-failure branches without a UIExtender: the co-op probe, the logger, and the two registration sinks
    /// (the whole assembly, or a filtered type list).
    /// </summary>
    internal static void Register(
        Assembly assembly,
        Func<bool> coopProbe,
        IModLogger logger,
        Action<Assembly> registerAssembly,
        Action<IReadOnlyList<Type>> registerTypes)
    {
        // SOLO TAKES THE ORIGINAL PATH, UNTOUCHED. Not an optimisation — a guarantee. Our own type
        // collection could in principle select a different set than UIExtenderEx's (its per-type
        // attribute read is tolerant where UIExtenderEx's is not), and a silently short list would
        // break TAOM's UI with no visible cause. Solo players are the overwhelming majority and must
        // not be exposed to that risk to serve a co-op path they never load, so the filtered path is
        // reached ONLY when a co-op module is present.
        bool coopActive;
        try
        {
            // Reading the flag here is SAFE, and that is verified rather than assumed.
            //
            // This is the one CoopPresence consumer that cannot self-correct: UI registration is a
            // one-shot, and a mixin cannot un-inject a widget that is already built. Everything else
            // (PatchShield, SaveShield, the diplomacy gates) reads live at gameplay time. So the
            // worry was that this decided from a probe taken before the launcher had published its
            // module list — which would silently take the solo branch and log nothing.
            //
            // Decompiled v1.4.7 settles it (two independent Codex passes, 2026-08-01):
            // Module.Initialize populates ModuleHelper's _loadedModules from the native module-code
            // string BEFORE calling LoadSubModules, which is what invokes OnSubModuleLoad. The list
            // is therefore complete here — even a SubModule constructor already sees it. The
            // "may not be populated this early" caution in CoopPresence.Refresh's docs is about the
            // pre-managed native string, not an OnSubModuleLoad race, and no extra Refresh() helps
            // with that. Removed the redundant re-probe accordingly.
            coopActive = coopProbe();
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                $"[CoopInterop] co-op probe failed ({ex.GetType().Name}: {ex.Message}) — " +
                "registering UI unfiltered (solo behaviour)");
            registerAssembly(assembly);
            return;
        }

        if (!coopActive)
        {
            // Logged unconditionally: this line's ABSENCE is the only way to tell a genuine solo
            // session from a co-op session whose detection ran too late, and the boot matrix has to
            // be able to tell those apart.
            logger.LogInfo(
                "[CoopInterop] no co-op module detected at UI registration — registering all TAOM UI");
            registerAssembly(assembly);
            return;
        }

        try
        {
            var candidates = CoopUiRegistrationPolicy.CollectUiExtensionTypes(assembly);
            var registered = CoopUiRegistrationPolicy.Filter(candidates, coopActive: true);
            var dropped = CoopUiRegistrationPolicy.Suppressed(candidates);

            logger.LogInfo(
                $"[CoopInterop] co-op active — registering {registered.Count} UI extension(s), " +
                $"suppressing {dropped.Count}" +
                (dropped.Count > 0 ? ": " + string.Join(", ", dropped.Select(t => t.Name)) : ""));

            registerTypes(registered);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                $"[CoopInterop] UI extension filtering failed ({ex.GetType().Name}: {ex.Message}) — " +
                "registering the full assembly unfiltered");
            registerAssembly(assembly);
        }
    }
}
