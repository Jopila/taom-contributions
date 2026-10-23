using System;
using SandBox.GauntletUI;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.InputSystem;
using TaleWorlds.ScreenSystem;

namespace TAOM.Features.LinesOfDescent.UI;

/// <summary>
/// The Lines of Descent popup (#905) as a layer of the screen that was on top when it opened. Every way out ends in
/// <see cref="Close"/>, which is safe to call twice: the close button and Escape, the clan screen leaving its Members tab
/// (only when opened from there), the host deactivating (a screen pushed over it), the encyclopedia changing page (a card
/// click, or the encyclopedia closing) and the host finalizing under the layer. The button, deactivation and page change only
/// set a flag; <see cref="Tick"/> acts on it in the same frame, on the UI thread after the movie updated, so the movie is
/// never released from inside its own update. A host that is not on top is not ticking this layer, so no check looks for that.
/// A layer is never reused.
/// </summary>
internal sealed class FamilyTreeLayer : GauntletLayer
{
    // The encyclopedia bar is 310 (EncyclopediaData.cs:175) and the clan screen's layer 1.
    private const int LayerOrder = 320;

    private readonly ScreenBase _host;
    private readonly bool _requireClanMembersTab;
    private readonly Action<FamilyTreeLayer> _onClosed;
    private GauntletMovieIdentifier? _movie;
    private FamilyTreePopupVM? _vm;
    private bool _subscribed;
    private bool _tornDown;
    private bool _closeRequested;

    public FamilyTreeLayer(ScreenBase host, bool requireClanMembersTab, Action<FamilyTreeLayer> onClosed)
        : base("TaomFamilyTree", LayerOrder)
    {
        _host = host;
        _requireClanMembersTab = requireClanMembersTab;
        _onClosed = onClosed;
    }

    /// <summary>Adds the layer to the host, builds the whole view model tree, loads the movie and takes focus.</summary>
    public void Show(FamilyTreeResult tree)
    {
        // The card frames, the tree lines and the element portraits live in this category; the clan screen has not loaded it.
        UIResourceManager.LoadSpriteCategory("ui_encyclopedia");
        InputRestrictions.SetInputRestrictions();
        // "Exit" (Escape) lives in GenericPanelGameKeyCategory, as in the Momentum popup.
        Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
        IsFocusLayer = true;
        // Added first: whatever throws below, Close can then remove the layer and nothing is left on the host.
        _host.AddLayer(this);
        _vm = new FamilyTreePopupVM(tree, RequestClose);
        _movie = LoadMovie("TaomFamilyTreePopup", _vm);
        Game.Current.EventManager.RegisterEvent<EncyclopediaPageChangedEvent>(OnEncyclopediaPageChanged);
        _subscribed = true;
        ScreenManager.TrySetFocus(this);
    }

    public void Close()
    {
        Teardown();
        if (!IsFinalized && _host.HasLayer(this))
            _host.RemoveLayer(this);
    }

    private void RequestClose() => _closeRequested = true;

    private void OnEncyclopediaPageChanged(EncyclopediaPageChangedEvent e) => _closeRequested = true;

    protected override void Tick(float dt)
    {
        if (_tornDown)
            return;
        base.Tick(dt);
        if (_closeRequested || Input.IsHotKeyReleased("Exit") || LeftClanMembersTab())
            Close();
    }

    protected override void OnDeactivate()
    {
        // A deactivated layer is not ticked: this closes it on the first tick after the host is active again.
        _closeRequested = true;
        base.OnDeactivate();
    }

    protected override void OnFinalize()
    {
        // The host finalizing walks its own layer list, so this path must not remove the layer again.
        Teardown();
        base.OnFinalize();
    }

    private bool LeftClanMembersTab() =>
        _requireClanMembersTab && _host is GauntletClanScreen clan && clan._dataSource?.IsMembersSelected != true;

    private void Teardown()
    {
        if (_tornDown)
            return;
        _tornDown = true;
        try
        {
            if (_subscribed)
            {
                Game.Current?.EventManager.UnregisterEvent<EncyclopediaPageChangedEvent>(OnEncyclopediaPageChanged);
                _subscribed = false;
            }

            InputRestrictions.ResetInputRestrictions();
            ScreenManager.TryLoseFocus(this);
            if (_movie != null)
            {
                ReleaseMovie(_movie);
                _movie = null;
            }

            _vm?.OnFinalize();
            _vm = null;
        }
        finally
        {
            _onClosed(this);
        }
    }
}
