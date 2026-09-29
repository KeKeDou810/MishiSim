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
        Activator.CreateInstance(TypeOf("CardDefinition"), new object[] { id, id, faction, 0, 1000, "", type, "", "", null, null, "" });
    private static bool Add(object deck, object card) => (bool)deck.GetType().GetMethod("TryAdd").Invoke(deck, new[] { card, null });
    [Test] public void TokensCannotBeAddedOrOfferedInDeckBuilding()
    {
        var deckType = TypeOf("DeckModel");
        var deck = Activator.CreateInstance(deckType, new object[] { null });
        var token = Card("token", "衍生物");
        Assert.AreEqual(true, token.GetType().GetProperty("IsToken").GetValue(token));
        Assert.AreEqual(false, deckType.GetMethod("IsLibraryEligible").Invoke(deck, new[] { token }));
        var args = new[] { token, null };
        Assert.AreEqual(false, deckType.GetMethod("TryAdd").Invoke(deck, args));
        StringAssert.Contains("衍生物", (string)args[1]);
        Assert.AreEqual(0, deckType.GetProperty("Count").GetValue(deck));
        Assert.IsTrue(Add(deck, Card("contract", "契约时魔")));
        Assert.AreEqual(false, deckType.GetMethod("IsLibraryEligible").Invoke(deck, new[] { token }));
        Assert.IsFalse(Add(deck, token));
        Assert.AreEqual(1, deckType.GetProperty("Count").GetValue(deck));
    }
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
    private object Version(string id, string rulesId) => Activator.CreateInstance(TypeOf("CardDefinition"), new object[] { Card(id), rulesId, "SP" });
    [Test] public void ContractFiltersLibraryWithoutHidingCardsAtCopyLimit()
    {
        var mode = Load("[{\"id\":\"test\",\"name\":\"Test\",\"cardLimits\":{\"banned\":0}}]").GetValue(0);
        var deck = Activator.CreateInstance(TypeOf("DeckModel"), mode);
        bool Visible(object card) => (bool)deck.GetType().GetMethod("IsLibraryEligible").Invoke(deck, new[] { card });
        Assert.IsTrue(Visible(Card("foreign", faction: "另一个国家")));
        Assert.IsTrue(Add(deck, Card("contract", "契约时魔")));
        Assert.IsTrue(Visible(Card("normal")));
        Assert.IsTrue(Visible(Card("generic", faction: "不明")));
        Assert.IsFalse(Visible(Card("foreign", faction: "另一个国家")));
        Assert.IsFalse(Visible(Card("contract", "契约时魔")));
        Assert.IsFalse(Visible(Card("player", "玩家卡")));
        Assert.IsFalse(Visible(Card("banned")));
        for (int i = 0; i < 4; i++) Assert.IsTrue(Add(deck, Card("normal")));
        Assert.IsTrue(Visible(Card("normal")));
        Assert.IsFalse(Add(deck, Card("normal")));
        mode.GetType().GetProperty("RequireSameFaction").SetValue(mode, false);
        Assert.IsTrue(Visible(Card("foreign", faction: "另一个国家")));
    }
    [Test] public void SharedRuleIdentityNeedsNoBasePrintingAndFailedReloadKeepsOldData()
    {
        string folder = path + "-cards";
        Directory.CreateDirectory(folder);
        string Lua(string id, string name = "Player") => "return {id='" + id + "',rulesId='shared-rule',name='" + name + "',type='玩家卡',faction='test',level=0,artworkPath='" + id + ".png'}";
        try
        {
            File.WriteAllText(Path.Combine(folder, "a.lua"), Lua("print-a"));
            File.WriteAllText(Path.Combine(folder, "b.lua"), Lua("print-b"));
            var db = Activator.CreateInstance(TypeOf("CardDatabase"));
            var load = db.GetType().GetMethod("LoadDirectory");
            load.Invoke(db, new object[] { folder });
            Assert.AreEqual(2, db.GetType().GetProperty("Count").GetValue(db));
            File.Delete(Path.Combine(folder, "a.lua"));
            load.Invoke(db, new object[] { folder });
            Assert.AreEqual(1, db.GetType().GetProperty("Count").GetValue(db));
            File.WriteAllText(Path.Combine(folder, "a.lua"), Lua("print-a", "Different"));
            Assert.Throws<TargetInvocationException>(() => load.Invoke(db, new object[] { folder }));
            Assert.AreEqual(1, db.GetType().GetProperty("Count").GetValue(db));
        }
        finally { Directory.Delete(folder, true); }
    }
    [Test] public void LuaReadsExplicitIdentityAndDefaultsLegacyIdentity()
    {
        var loader = TypeOf("LuaCardLoader").GetMethod("Load");
        string source = "return {id='alternate',name='Player',type='玩家卡',faction='test',level=0,artworkPath='alternate.png'";
        var card = loader.Invoke(null, new object[] { source + ",rulesId='base',rarity='SP'}" });
        Assert.AreEqual("base", card.GetType().GetProperty("RulesId").GetValue(card));
        Assert.AreEqual("alternate.png", card.GetType().GetProperty("ArtworkPath").GetValue(card));
        var legacy = loader.Invoke(null, new object[] { source + "}" });
        Assert.AreEqual("alternate", legacy.GetType().GetProperty("RulesId").GetValue(legacy));
    }
    [Test] public void AlternatePrintingsShareLimitButKeepTheirEntries()
    {
        var mode = Load("[{\"id\":\"test\",\"name\":\"Test\"}]").GetValue(0);
        var deck = Activator.CreateInstance(TypeOf("DeckModel"), mode);
        Assert.IsTrue(Add(deck, Card("contract", "契约时魔")));
        Assert.IsTrue(Add(deck, Card("base")));
        for (int i = 0; i < 3; i++) Assert.IsTrue(Add(deck, Version("alternate", "base")));
        Assert.IsFalse(Add(deck, Card("base")));
        Assert.IsFalse(Add(deck, Version("another", "base")));
        Assert.IsTrue((bool)deck.GetType().GetMethod("TryRemove").Invoke(deck, new object[] { "alternate", null }));
        Assert.IsTrue(Add(deck, Card("base")));
        Assert.AreEqual("alternate", Version("alternate", "base").GetType().GetProperty("Id").GetValue(Version("alternate", "base")));
    }
    [Test] public void CanonicalBanAppliesToAlternatePrinting()
    {
        var mode = Load("[{\"id\":\"test\",\"name\":\"Test\",\"cardLimits\":{\"base\":0}}]").GetValue(0);
        var deck = Activator.CreateInstance(TypeOf("DeckModel"), mode);
        Assert.IsTrue(Add(deck, Card("contract", "契约时魔")));
        Assert.IsFalse(Add(deck, Version("alternate", "base")));
    }
    [TestCase("[{\"id\":\"test\",\"name\":\"Test\",\"maxCopies\":999}]")]
    [TestCase("[{\"id\":\"test\",\"name\":\"Test\",\"firstTurnSkip\":[\"Main\"]}]")]
    [TestCase("[{\"id\":\"test\",\"name\":\"Test\",\"typo\":1}]")]
    [TestCase("[{\"id\":\"test\",\"name\":\"Test\"},{\"id\":\"test\",\"name\":\"Duplicate\"}]")]
    public void RejectInvalidConfiguration(string modes) => Assert.Throws<TargetInvocationException>(() => Load(modes));
}
