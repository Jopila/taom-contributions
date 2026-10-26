namespace TAOM.Features.NotableServices;

/// <summary>The live (MCM over default) switch for the three notable services.</summary>
public interface INotableServicesSettingsProvider
{
    bool Enabled { get; }
}
