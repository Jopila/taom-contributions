using System;

namespace TAOM.Features.HardQuestions;

/// <summary>One validated config value: its name, its rule and how to read and write it.</summary>
internal sealed class HardQuestionsField
{
    public HardQuestionsField(string name, HardQuestionsFieldKind kind, Func<HardQuestionsConfig, float> get,
        Action<HardQuestionsConfig, float> set)
    {
        Name = name;
        Kind = kind;
        Get = get;
        Set = set;
    }

    public string Name { get; }

    public HardQuestionsFieldKind Kind { get; }

    public Func<HardQuestionsConfig, float> Get { get; }

    public Action<HardQuestionsConfig, float> Set { get; }
}
