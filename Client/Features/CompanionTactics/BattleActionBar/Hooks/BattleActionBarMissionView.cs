using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CompanionTactics.BattleActionBar.UI;

namespace TAOM.Features.CompanionTactics.BattleActionBar.Hooks;

/// <summary>
/// MissionView that owns the BattleActionBar GauntletLayer lifecycle. Built by the engine as a
/// <c>[DefaultView]</c> in every mission with a screen (MissionScreen.OnMissionAfterStarting registers it), because a
/// view TAOM adds in OnMissionBehaviorInitialize is never registered and never ticks (#970). Attaches in field
/// battles (<c>Mission.IsFieldBattle</c>) on the first screen tick once the mode is Battle: the mode is Deployment at
/// screen init (DeploymentHandler.AfterStart). Refreshes the action bar VM at most twice per second based on the
/// player's selected formation.
/// </summary>
[DefaultView]
public sealed class BattleActionBarMissionView : MissionView
{
    private const float RefreshIntervalSec = 0.5f;
    private const int MaxHotkeys = 9;

    private ICompanionTacticsSettingsProvider? _settings;
    private IBattleActionBarService? _service;
    private ITroopStanceManager? _stances;
    private IModLogger? _logger;

    private GauntletLayer? _layer;
    private BattleActionBarVM? _vm;
    private bool _isInitialized;
    private bool _attachAttempted;
    private float _refreshAccumulator;
    private Formation? _lastFormation;

    public override void OnMissionScreenInitialize()
    {
        base.OnMissionScreenInitialize();
        _settings = IoC.Resolve<ICompanionTacticsSettingsProvider>();
        _service = IoC.Resolve<IBattleActionBarService>();
        _stances = IoC.Resolve<ITroopStanceManager>();
        _logger = IoC.Resolve<IModLogger>();

        if (Mission.IsFieldBattle)
        {
            _logger?.LogInfo($"[BattleActionBar] screen init: MissionScreen={(MissionScreen != null ? "set" : "NULL")} mode={Mission.Mode} enabled={_settings?.EnableBattleActionBar}");
        }
    }

    private void TryAttachLayer()
    {
        if (_service == null || _stances == null) return;

        try
        {
            _vm = new BattleActionBarVM(_service, _stances);
            _layer = new GauntletLayer("GauntletLayer", 100, false);
            // Required: without InputRestrictions the layer renders but never registers with the
            // MissionScreen's input dispatcher — mouse clicks on the action-bar buttons silently
            // fall through. The hotkey path via HandleHotkeyInput continues to work either way
            // (it polls Mission.InputManager directly), which is why this latent bug was masked
            // until #225. Same fix class as #202 / d141304.
            _layer.InputRestrictions.SetInputRestrictions();
            _layer.LoadMovie("BattleActionBar", _vm);
            MissionScreen.AddLayer(_layer);
            _isInitialized = true;
            _logger?.LogInfo("[BattleActionBar] mission init — layer attached");
        }
        catch (System.Exception ex)
        {
            _logger?.LogError($"[BattleActionBar] layer attach failed: {ex.Message}");
        }
    }

    public override void OnMissionScreenTick(float dt)
    {
        base.OnMissionScreenTick(dt);
        if (_settings == null) return;
        if (!_isInitialized)
        {
            // Mode is Deployment at screen init in a field battle (DeploymentHandler.cs:41): attach on the first Battle tick.
            if (_attachAttempted || !ShouldAttach(_settings.EnableBattleActionBar, Mission.IsFieldBattle, Mission.Mode)) return;
            _attachAttempted = true;
            TryAttachLayer();
            if (!_isInitialized) return;
        }
        if (_vm == null || !_settings.EnableBattleActionBar) return;

        _refreshAccumulator += dt;
        if (_refreshAccumulator >= RefreshIntervalSec)
        {
            _refreshAccumulator = 0f;
            RefreshFromSelectedFormation();
        }

        HandleHotkeyInput();
    }

    public override void OnMissionScreenFinalize()
    {
        if (_isInitialized)
        {
            try
            {
                _stances?.ClearAllStances();
                if (_layer != null)
                {
                    _layer.InputRestrictions.ResetInputRestrictions();
                    MissionScreen?.RemoveLayer(_layer);
                }
            }
            catch (System.Exception ex) { _logger?.LogWarning($"[BattleActionBar] cleanup: {ex.Message}"); }
            _vm?.OnFinalize();
            _vm = null;
            _layer = null;
            _isInitialized = false;
        }
        base.OnMissionScreenFinalize();
    }

    private void RefreshFromSelectedFormation()
    {
        var team = Mission.Current?.PlayerTeam;
        var orderController = team?.PlayerOrderController;
        var selected = orderController?.SelectedFormations;
        var formation = (selected != null && selected.Count > 0) ? selected[0] : null;

        // P3-1 fix (Codex review #36, 2026-05-06): the original code short-circuited when
        // formation == _lastFormation, which prevented the bar from picking up external
        // stance changes (Patch35_Formation_SetMovementOrder cleared the stance but the
        // visible button stayed highlighted). Lift the short-circuit; on same-formation
        // refresh we still rebuild buttons + re-sync IsActive from the stance manager,
        // which is cheap (≤9 buttons) and runs at most twice per second.
        _lastFormation = formation;
        var adapter = formation != null ? new FormationAdapter(formation) : null;
        _vm!.UpdateForFormation(formation, adapter);
    }

    private void HandleHotkeyInput()
    {
        if (Mission.Current?.InputManager == null) return;

        for (var i = 1; i <= MaxHotkeys; i++)
        {
            var key = NumKey(i);
            if (key == InputKey.Invalid) continue;
            if (Mission.Current.InputManager.IsKeyPressed(key))
            {
                var button = _vm?.TryGetActionByHotkeyIndex(i);
                button?.ExecuteAction();
                // After the action mutates stance state, re-derive each button's IsActive
                // so the visible highlight matches the stance manager (P3-1).
                _vm?.SyncActiveFromStance();
            }
        }
    }

    private static InputKey NumKey(int index) => index switch
    {
        1 => InputKey.D1,
        2 => InputKey.D2,
        3 => InputKey.D3,
        4 => InputKey.D4,
        5 => InputKey.D5,
        6 => InputKey.D6,
        7 => InputKey.D7,
        8 => InputKey.D8,
        9 => InputKey.D9,
        _ => InputKey.Invalid,
    };

    internal static bool ShouldAttach(bool enabled, bool isFieldBattle, MissionMode mode) =>
        enabled && isFieldBattle && mode == MissionMode.Battle;
}
