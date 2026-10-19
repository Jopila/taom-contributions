using System.Collections.Generic;
using System.IO;

namespace TAOM.Adapters;

public sealed class ShaderSourceFileSystem : IShaderSourceFileSystem
{
    public IReadOnlyList<string> ListFiles(string folder) =>
        Directory.Exists(folder) ? Directory.GetFiles(folder) : new string[0];

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    public void WriteAllBytes(string path, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllBytes(path, bytes);
    }

    public bool FileExists(string path) => File.Exists(path);
}
