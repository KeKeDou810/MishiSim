using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

public sealed class ExternalContentPathTests
{
    private string root;
    [SetUp] public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "mishi-content-path-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        Directory.CreateDirectory(Path.Combine(root, "ProjectSettings"));
    }
    [TearDown] public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    // Unity asmdef test assemblies cannot reference predefined Assembly-CSharp directly.
    private static string Resolve(string path, bool editor)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ExternalContentPath"))
            .First(t => t != null);
        return (string)type.GetMethod("Resolve").Invoke(null, new object[] { path, editor });
    }
    [Test] public void MainEditorUsesProjectContent() =>
        Assert.AreEqual(Path.Combine(root, "Content"), Resolve(Path.Combine(root, "Assets"), true));
    [Test] public void VirtualPlayerUsesOriginalContent() =>
        Assert.AreEqual(Path.Combine(root, "Content"), Resolve(Path.Combine(root, "Library", "VP", "mppm123", "Assets"), true));
    [Test] public void VirtualPlayerIgnoresStaleCopiedContent()
    {
        string clone = Path.Combine(root, "Library", "VP", "mppm123");
        Directory.CreateDirectory(Path.Combine(clone, "Content", "Cards"));
        Assert.AreEqual(Path.Combine(root, "Content"), Resolve(Path.Combine(clone, "Assets"), true));
    }
    [Test] public void StandaloneKeepsContentNextToExecutable() =>
        Assert.AreEqual(Path.Combine(root, "Build", "Content"), Resolve(Path.Combine(root, "Build", "Mishi_Data"), false));
    [Test] public void PlayerNeverAppliesEditorVirtualProjectFallback()
    {
        string build = Path.Combine(root, "Library", "VP", "my-build");
        Assert.AreEqual(Path.Combine(build, "Content"), Resolve(Path.Combine(build, "Mishi_Data"), false));
    }
}
