using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>File access seam for the shader source install, so the logic runs without a disk.</summary>
public interface IShaderSourceFileSystem
{
    /// <summary>The full paths of the files directly in the folder; empty when the folder is missing.</summary>
    IReadOnlyList<string> ListFiles(string folder);

    byte[] ReadAllBytes(string path);

    /// <summary>Writes the file, creating the target directory first when it is missing.</summary>
    void WriteAllBytes(string path, byte[] bytes);

    bool FileExists(string path);
}
