namespace TAOM.Features.HardQuestions;

/// <summary>
/// The MCM toggle over a default of on. <c>TaomSettings.Instance</c> is null early in startup and when MCM
/// fails to load, so the read falls back to on.
/// </summary>
public sealed class HardQuestionsSettingsProvider : IHardQuestionsSettingsProvider
{
    public bool IsEnabled => TaomSettings.Instance?.EnableHardQuestions ?? true;
}
