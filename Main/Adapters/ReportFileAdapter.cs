using System;
using System.IO;
using System.Text;

namespace TAOM.Adapters;

/// <summary>Boundary implementation of <see cref="IReportFileAdapter"/>: a UTF-8 file in the game's working-directory Logs folder, where FileLogger writes taom_debug.</summary>
public class ReportFileAdapter : IReportFileAdapter
{
    private const string LogDirectory = "Logs";

    public string Write(string baseName, string content)
    {
        Directory.CreateDirectory(LogDirectory);
        var path = Path.Combine(LogDirectory, $"{baseName}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return Path.GetFullPath(path);
    }
}
