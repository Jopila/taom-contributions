namespace TAOM.Features.Provender;

/// <summary>The "Provender-master" MCM group, clamped. Read live whenever an order is judged.</summary>
public interface IProvenderSettingsProvider
{
    ProvenderSettings Get();
}
