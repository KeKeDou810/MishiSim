using System;
using System.Linq;
using NUnit.Framework;

public sealed class CardSearchTests
{
    private static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Card(string type = "通常时魔", int power = 2000, int level = 1) => Activator.CreateInstance(TypeOf("CardDefinition"), new object[] {
        "PD01-003SP", "远星小队的机械师 卡莲娜", "星河联盟", level, power, "未来视", type, "星河兵", "art.png", null, null, "被战斗破坏时抽1张卡" });
    [TestCase(null, null, true)]
    [TestCase(1, 1, true)]
    [TestCase(2, null, false)]
    [TestCase(null, 0, false)]
    public void TimeBoundsIncludeEdgesAndAllowOneSidedRange(int? min, int? max, bool expected) =>
        Assert.AreEqual(expected, TypeOf("CardSearch").GetMethod("MatchesTime").Invoke(null, new object[] { Card("决策卡"), min, max }));
    [TestCase(false, false, "契约时魔,通常时魔,决策卡")]
    [TestCase(false, true, "决策卡,通常时魔,契约时魔")]
    [TestCase(true, false, "契约时魔,决策卡,通常时魔")]
    [TestCase(true, true, "通常时魔,决策卡,契约时魔")]
    public void OrganizeByTypeOrTimeAndReverse(bool byTime, bool reverse, string expected)
    {
        var cards = Array.CreateInstance(TypeOf("CardDefinition"), 3);
        cards.SetValue(Card("决策卡", level: 1), 0);
        cards.SetValue(Card("通常时魔", level: 3), 1);
        cards.SetValue(Card("契约时魔", level: 0), 2);
        var result = (System.Collections.IEnumerable)TypeOf("CardSearch").GetMethod("Organize").Invoke(null, new object[] { cards, byTime, reverse });
        Assert.AreEqual(expected, string.Join(",", result.Cast<object>().Select(card => card.GetType().GetProperty("Type").GetValue(card))));
    }
    private static int Score(string query) => (int)TypeOf("CardSearch").GetMethod("Score").Invoke(null, new[] { Card(), query });
    [TestCase("机械卡莲")]
    [TestCase("pd01003sp")]
    [TestCase("ＰＤ０１")]
    [TestCase("星河 抽1")]
    [TestCase("未来视")]
    public void MatchesPartialNormalizedAndMultipleTerms(string query) => Assert.GreaterOrEqual(Score(query), 0);
    [Test] public void RejectsUnmatchedTerm() => Assert.AreEqual(-1, Score("机械 不存在的词"));
    [Test] public void ExactIdRanksAboveFuzzyId() => Assert.Less(Score("PD01-003SP"), Score("P13SP"));
    [Test] public void EmptyQueryMatches() => Assert.AreEqual(0, Score(" "));
    [TestCase("", "", true)]
    [TestCase("0", "2000", true)]
    [TestCase("3000", "2000", false)]
    [TestCase("-1", "", false)]
    [TestCase("abc", "", false)]
    [TestCase("999999999999", "", false)]
    public void ValidatesPowerRange(string min, string max, bool expected)
    {
        Assert.AreEqual(expected, TypeOf("CardSearch").GetMethod("TryPowerRange").Invoke(null, new object[] { min, max, null, null }));
    }
    [TestCase("通常时魔", 2000, 2000, 2000, true)]
    [TestCase("通常时魔", 1000, 2000, 3000, false)]
    [TestCase("决策卡", 0, 0, 3000, false)]
    [TestCase("玩家卡", 0, 0, 3000, false)]
    public void PowerBoundsInclusiveAndStatlessCardsExcluded(string type, int power, int min, int max, bool expected)
    {
        Assert.AreEqual(expected, TypeOf("CardSearch").GetMethod("MatchesPower").Invoke(null, new object[] { Card(type, power), min, max }));
    }
}
