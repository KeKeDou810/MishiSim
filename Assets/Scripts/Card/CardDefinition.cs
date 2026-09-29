public sealed class CardDefinition
{
    public string Id { get; }
    public string ScriptSource { get; internal set; }
    public Mishi.Battle.ClockKind Clock { get; internal set; }
    // Rule identity shared by alternate printings; Id remains the artwork/version identity.
    public string RulesId { get; }
    public string Rarity { get; }
    public string Name { get; }
    public string Faction { get; }
    public int Level { get; }
    public int Power { get; }
    public string Sign { get; }
    public string Type { get; }
    public string Race { get; }
    public string ArtworkPath { get; }
    public string EffectText { get; }
    public bool IsContract => Type == "契约时魔";
    public bool IsPlayer => Type == "玩家卡";
    public bool IsDecision => Type == "决策卡";
    public bool IsToken => Type == "衍生物";
    public string PlayerCardId1 { get; }
    public string PlayerCardId2 { get; }

    public CardDefinition(string id, string name, string faction, int level, int power, string sign, string type, string race, string artworkPath, string playerCardId1 = null, string playerCardId2 = null, string effectText = "")
    {
        Id = id;
        RulesId = id;
        Rarity = string.Empty;
        Name = name;
        Faction = faction;
        Level = level;
        Power = power;
        Sign = sign;
        Type = type;
        Race = race;
        ArtworkPath = artworkPath;
        EffectText = effectText ?? string.Empty;
        PlayerCardId1 = playerCardId1;
        PlayerCardId2 = playerCardId2;
    }
    public CardDefinition(CardDefinition source, string rulesId, string rarity)
        : this(source.Id, source.Name, source.Faction, source.Level, source.Power, source.Sign,
            source.Type, source.Race, source.ArtworkPath, source.PlayerCardId1, source.PlayerCardId2, source.EffectText)
    {
        ScriptSource = source.ScriptSource; Clock = source.Clock;
        RulesId = string.IsNullOrWhiteSpace(rulesId) ? source.Id : rulesId.Trim();
        Rarity = rarity ?? string.Empty;
    }
}
