namespace TAOM.Adapters;

/// <summary>Writes a report file in the game's Logs folder.</summary>
public interface IReportFileAdapter
{
    /// <summary>Writes <paramref name="content"/> to a new timestamped file named from <paramref name="baseName"/> and returns its full path.</summary>
    string Write(string baseName, string content);
}
