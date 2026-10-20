using System;

namespace TAOM.Features.LordCourtesies;

/// <summary>One validated config value: its name, its rule and how to read and write it.</summary>
internal sealed class LordCourtesiesField
{
    public LordCourtesiesField(string name, LordCourtesiesFieldKind kind, Func<LordCourtesiesConfig, float> get,
        Action<LordCourtesiesConfig, float> set)
    {
        Name = name;
        Kind = kind;
        Get = get;
        Set = set;
    }

    public string Name { get; }

    public LordCourtesiesFieldKind Kind { get; }

    public Func<LordCourtesiesConfig, float> Get { get; }

    public Action<LordCourtesiesConfig, float> Set { get; }
}
