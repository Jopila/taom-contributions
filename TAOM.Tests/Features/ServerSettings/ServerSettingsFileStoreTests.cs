using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;

namespace TAOM.Tests.Features.ServerSettings;

/// <summary>The only file IO of the feature: path rule, read, and the two write-once rules. Real temp directories.</summary>
[TestClass]
public class ServerSettingsFileStoreTests
{
    private string _root = null!;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "taom-server-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private string Dir(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static ServerSettingsFileStore Store(string? userDir, string? moduleRoot, string cwd) =>
        new ServerSettingsFileStore(name => name == "BANNERLORD_USER_DIR" ? userDir : null, moduleRoot, cwd);

    [TestMethod]
    public void Path_UsesBannerlordUserDir()
    {
        var user = Dir("user");

        var where = Store(user, Dir("module"), Dir("cwd")).Locate();

        Assert.AreEqual(Path.Combine(user, "taom-server-settings.json"), where.Path);
        Assert.AreEqual(Path.Combine(user, "taom-server-settings.defaults.json"), where.DefaultsPath);
        Assert.AreEqual("BANNERLORD_USER_DIR", where.Rule);
    }

    [TestMethod]
    public void Path_FallsBackToModuleRoot_WhenUnsetOrBlank()
    {
        var module = Dir("module");

        foreach (var user in new string?[] { null, "", "   " })
        {
            var where = Store(user, module, Dir("cwd")).Locate();

            Assert.AreEqual(Path.Combine(module, "taom-server-settings.json"), where.Path);
            Assert.AreEqual("TAOM module folder", where.Rule);
        }
    }

    [TestMethod]
    public void Path_FallsBackToCurrentDirectory_WhenThereIsNoModuleRoot()
    {
        var cwd = Dir("cwd");

        var where = Store(null, null, cwd).Locate();

        Assert.AreEqual(Path.Combine(cwd, "taom-server-settings.json"), where.Path);
        Assert.AreEqual("current directory", where.Rule);
    }

    [TestMethod]
    public void TryRead_MissingFile_IsReportedMissing()
    {
        var store = Store(Dir("user"), null, Dir("cwd"));

        var read = store.TryRead(store.Locate().Path);

        Assert.AreEqual(ServerFileState.Missing, read.State);
    }

    [TestMethod]
    public void TryRead_ReturnsTheText()
    {
        var user = Dir("user");
        var store = Store(user, null, Dir("cwd"));
        File.WriteAllText(store.Locate().Path, "{ \"a\": 1 }", new UTF8Encoding(false));

        var read = store.TryRead(store.Locate().Path);

        Assert.AreEqual(ServerFileState.Loaded, read.State);
        Assert.AreEqual("{ \"a\": 1 }", read.Text);
    }

    [TestMethod]
    public void ReadIOException_AllDefaultsWithError()
    {
        var store = Store(Dir("user"), null, Dir("cwd"));
        // A directory where the file should be: reading it throws, which must come back as a state, not an exception.
        Directory.CreateDirectory(store.Locate().Path);

        var read = store.TryRead(store.Locate().Path);

        Assert.AreEqual(ServerFileState.Unreadable, read.State);
        Assert.IsFalse(string.IsNullOrEmpty(read.Error));
    }

    [TestMethod]
    public void MissingFile_WritesTemplateOnce()
    {
        var store = Store(Dir("user"), null, Dir("cwd"));
        var path = store.Locate().Path;

        Assert.IsNull(store.WriteIfMissing(path, "first"));
        Assert.IsNull(store.WriteIfMissing(path, "second"));

        Assert.AreEqual("first", File.ReadAllText(path));
    }

    [TestMethod]
    public void ExistingFile_IsNeverRewritten()
    {
        var store = Store(Dir("user"), null, Dir("cwd"));
        var path = store.Locate().Path;
        File.WriteAllText(path, "operator edits");

        Assert.IsNull(store.WriteIfMissing(path, "template"));

        Assert.AreEqual("operator edits", File.ReadAllText(path));
    }

    [TestMethod]
    public void WriteIfMissing_CreationRace_KeepsTheOtherWritersFile()
    {
        // Another process creates the file between the existence check and CreateNew.
        var user = Dir("user");
        var store = new ServerSettingsFileStore(_ => user, null, Dir("cwd"), p =>
        {
            File.WriteAllText(p, "other writer");
            throw new IOException("The file exists.");
        });
        var path = store.Locate().Path;

        Assert.IsNull(store.WriteIfMissing(path, "template"));

        Assert.AreEqual("other writer", File.ReadAllText(path));
    }

    [TestMethod]
    public void WriteIfMissing_WriteFailsAfterCreate_DeletesThePartialFile_AndReportsTheError()
    {
        // This process creates the file, then the write fails (a full disk, a quota).
        var user = Dir("user");
        var store = new ServerSettingsFileStore(_ => user, null, Dir("cwd"), p =>
        {
            File.WriteAllText(p, "");
            return new FailingWriteStream();
        });
        var path = store.Locate().Path;

        var error = store.WriteIfMissing(path, "template");

        Assert.IsFalse(string.IsNullOrEmpty(error), "a failed write must not report success");
        Assert.IsFalse(File.Exists(path), "the empty or partial template must not survive to the next boot");
    }

    private sealed class FailingWriteStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("There is not enough space on the disk.");
    }

    [TestMethod]
    public void DefaultsReference_RewrittenOnlyWhenStale()
    {
        var store = Store(Dir("user"), null, Dir("cwd"));
        var path = store.Locate().DefaultsPath;

        Assert.IsNull(store.WriteIfChanged(path, "v1"));
        var stamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, stamp);

        Assert.IsNull(store.WriteIfChanged(path, "v1"));
        Assert.AreEqual(stamp, File.GetLastWriteTimeUtc(path), "identical content must not touch the file");

        Assert.IsNull(store.WriteIfChanged(path, "v2"));
        Assert.AreEqual("v2", File.ReadAllText(path));
    }

    [TestMethod]
    public void UnwritableDir_WarnsAndContinues()
    {
        // The "directory" is a file, so creating it and writing under it both fail.
        var blocker = Path.Combine(_root, "blocker");
        File.WriteAllText(blocker, "x");
        var store = Store(blocker, null, Dir("cwd"));

        var missing = store.WriteIfMissing(store.Locate().Path, "t");
        var changed = store.WriteIfChanged(store.Locate().DefaultsPath, "t");

        Assert.IsFalse(string.IsNullOrEmpty(missing));
        Assert.IsFalse(string.IsNullOrEmpty(changed));
    }
}
