namespace TAOM.Features.HardQuestions;

/// <summary>Live (MCM over default) switch for the feature.</summary>
public interface IHardQuestionsSettingsProvider
{
    bool IsEnabled { get; }
}
