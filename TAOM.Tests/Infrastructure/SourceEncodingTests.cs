using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Every C# source file is valid UTF-8. A builder once wrote a Windows-1252 em dash (byte 0x97) into
/// an otherwise UTF-8 file; the compiler accepted it and only an unrelated tool test, reading the file
/// as UTF-8, failed (#966, docs/reviews/rca-coop-player-attribution-2026-10-21.md).
/// </summary>
[TestClass]
public class SourceEncodingTests
{
    private static readonly string[] SourceRoots = { "Main", "Client", "Dependencies", "TAOM.Tests" };

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    internal static bool IsValidUtf8(byte[] bytes)
    {
        try
        {
            StrictUtf8.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    [TestMethod]
    public void IsValidUtf8_Windows1252EmDash_ReturnsFalse()
    {
        var bytes = Encoding.ASCII.GetBytes("// a dash ").Concat(new byte[] { 0x97 }).ToArray();

        Assert.IsFalse(IsValidUtf8(bytes));
    }

    [TestMethod]
    public void IsValidUtf8_Utf8EmDash_ReturnsTrue()
    {
        Assert.IsTrue(IsValidUtf8(Encoding.UTF8.GetBytes("// a dash — in UTF-8")));
    }

    [TestMethod]
    public void EveryCSharpSourceFile_IsValidUtf8()
    {
        var invalid = new List<string>();
        var scanned = 0;
        foreach (var root in SourceRoots)
        {
            foreach (var path in Directory.EnumerateFiles(RepoPath(root), "*.cs", SearchOption.AllDirectories))
            {
                var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (parts.Contains("bin") || parts.Contains("obj"))
                    continue;

                scanned++;
                if (!IsValidUtf8(File.ReadAllBytes(path)))
                    invalid.Add(path);
            }
        }

        Assert.IsTrue(scanned > 1000, $"Scanned only {scanned} files: the source roots did not resolve.");
        Assert.AreEqual(0, invalid.Count,
            "Not valid UTF-8 (convert the stray bytes, for example a Windows-1252 dash, to UTF-8):\n"
            + string.Join("\n", invalid));
    }
}
