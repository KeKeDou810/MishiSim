using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public sealed class GameModeRulesTests
{
    private string path;
    private static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    [SetUp] public void Setup() { path = Path.Combine(Path.GetTempPath(), "mishi-mode-" + Guid.NewGuid().ToString("N") + ".json"); }
    [TearDown] public void Cleanup() { if (File.Exists(path)) File.Delete(path); }
    private Array Load(string modes)
    {
        File.WriteAllText(path, "{\"version\":1,\"modes\":" + modes + "}");
        return (Array)TypeOf("GameMode").GetMethod("Load").Invoke(null, new object[] { path });
    }
    private object Card(string id, string type = "通常时魔", string faction = "星河联盟") =>
        Activator.CreateInstance(TypeOf("CardDefinition"), new object[] { id, id, faction, 0, 1000, "", type, "", "", Enum.ToObject(TypeOf("CardColor"), 1), null, null, "" });
    private static bool Add(object deck, object card) => (bool)deck.GetType().GetMethod("TryAdd").Invoke(deck, new[] { card, null });
    [Test] public void StandardAndTestHaveSeparateCopyLimitsAndCompleteValidation()
    {
        var modes = Load("[{\"id\":\"standard\",\"name\":\"标准\",\"maxCopies\":4},{\"id\":\"test\",\"name\":\"测试\",\"maxCopies\":49}]");
        foreach (int index in new[] { 0, 1 })
        {
            var deck = Activator.CreateInstance(TypeOf("DeckModel"), modes.GetValue(index));
            Assert.IsFalse(Add(deck, Card("normal")));
            Assert.IsTrue(Add(deck, Card("contract", "契约时魔")));
            Assert.IsFalse(Add(deck, Card("other-contract", "契约时魔")));
            Assert.IsFalse(Add(deck, Card("player", "玩家卡")));
            Assert.IsFalse(Add(deck, Card("enemy", faction: "另一个国家")));
            int copies = index == 0 ? 4 : 49;
            for (int i = 0; i < copies; i++) Assert.IsTrue(Add(deck, Card("normal")));
            Assert.IsFalse(Add(deck, Card("normal")));
            Assert.AreEqual(index == 1, deck.GetType().GetMethod("ValidateComplete").Invoke(deck, new object[] { null }));
        }
    }
    [Test] public void ModeSpecificBanAndFactionToggleAreApplied()
    {
        var mode = Load("[{\"id\":\"test\",\"name\":\"Test\",\"requireSameFaction\":false,\"cardLimits\":{\"banned\":0}}]").GetValue(0);
        var deck = Activator.CreateInstance(TypeOf("DeckModel"), mode);
        Assert.IsTrue(Add(deck, Card("contract", "契约时魔")));
        Assert.IsTrue(Add(deck, Card("foreign", faction: "另一个国家")));
        Assert.IsFalse(Add(deck, Card("banned")));
    }
    [TestCase("[{\"id\":\"test\",\"name\":\"Test\",\"maxCopies\":999}]")]
    [TestCase("[{\"id\":\"test\",\"name\":\"Test\",\"firstTurnSkip\":[\"Main\"]}]")]
    [TestCase("[{\"id\":\"test\",\"name\":\"Test\",\"typo\":1}]")]
    [TestCase("[{\"id\":\"test\",\"name\":\"Test\"},{\"id\":\"test\",\"name\":\"Duplicate\"}]")]
    public void RejectInvalidConfiguration(string modes) => Assert.Throws<TargetInvocationException>(() => Load(modes));
}
