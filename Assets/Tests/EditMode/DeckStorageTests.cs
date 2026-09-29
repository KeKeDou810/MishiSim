using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public sealed class DeckStorageTests
{
    private string directory;
    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(string name, params object[] args) => T("DeckStorage").GetMethod(name).Invoke(null, args);
    private object Deck()
    {
        var mode = Activator.CreateInstance(T("GameMode"));
        T("GameMode").GetProperty("Id").SetValue(mode, "standard");
        return Activator.CreateInstance(T("DeckModel"), new object[] { mode });
    }
    [SetUp] public void Setup() { directory = Path.Combine(Path.GetTempPath(), "mishi-storage-test-" + Guid.NewGuid().ToString("N")); }
    [TearDown] public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    [Test] public void EmptyDraftRoundTripsAndCanBeOverwritten()
    {
        Call("Save", directory, "测试卡组", Deck());
        Call("Save", directory, "测试卡组", Deck());
        CollectionAssert.AreEqual(new[] { "测试卡组" }, (string[])Call("List", directory));
        var data = Call("Read", directory, "测试卡组");
        Assert.AreEqual(1, data.GetType().GetField("Version").GetValue(data));
        Assert.AreEqual(1, Directory.GetFiles(directory).Length);
    }
    [TestCase("../escape")]
    [TestCase("..\\escape")]
    [TestCase("")]
    [TestCase(" spaced ")]
    public void RejectsUnsafeNames(string name) => Assert.Throws<TargetInvocationException>(() => Call("Save", directory, name, Deck()));
    [TestCase("{broken")]
    [TestCase("{\"Version\":2,\"ModeId\":\"standard\",\"Cards\":{}}")]
    [TestCase("{\"Version\":1,\"ModeId\":\"standard\",\"Cards\":{\"card\":-1}}")]
    public void RejectsCorruptOrInvalidFiles(string json)
    {
        Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, "deck-test.json"), json);
        Assert.Throws<TargetInvocationException>(() => Call("Read", directory, "test"));
    }
}
