using System;
using TaleWorlds.Library;

namespace TAOM.Features.LinesOfDescent.UI;

/// <summary>The data source of TaomFamilyTreePopup.xml: the title, the root of the node tree and the close command.</summary>
internal sealed class FamilyTreePopupVM : ViewModel
{
    private readonly Action _close;

    public FamilyTreePopupVM(FamilyTreeResult result, Action close)
    {
        _close = close;
        Title = FamilyTreeTexts.Title(result.RootId).ToString();
        Tree = new FamilyTreeNodeVM(result.Root);
    }

    [DataSourceProperty]
    public string Title { get; }

    [DataSourceProperty]
    public FamilyTreeNodeVM Tree { get; }

    public void ExecuteClose() => _close();

    public override void OnFinalize()
    {
        base.OnFinalize();
        Tree.OnFinalize();
    }
}
