public enum CardColor { Unknown, Blue, Green, Red, White, Black, Generic }

public sealed class CardDefinition
{
    public string Id { get; }
    public string Name { get; }
    public string Faction { get; }
    public int Level { get; }
    public int Power { get; }
    public string Sign { get; }
    public string Type { get; }
    public string Race { get; }
    public string ArtworkPath { get; }
    public string EffectText { get; }
    public CardColor Color { get; }
    public string ColorName
    {
        get
        {
            switch (Color)
            {
                case CardColor.Blue: return "蓝色";
                case CardColor.Green: return "绿色";
                case CardColor.Red: return "红色";
                case CardColor.White: return "白色";
                case CardColor.Black: return "黑色";
                case CardColor.Generic: return "泛用";
                default: return "未配置";
            }
        }
    }
    public bool IsContract => Type == "契约时魔";
    public bool IsPlayer => Type == "玩家卡";
    public bool IsDecision => Type == "决策卡";
    public string PlayerCardId1 { get; }
    public string PlayerCardId2 { get; }

    public CardDefinition(string id, string name, string faction, int level, int power, string sign, string type, string race, string artworkPath, CardColor color = CardColor.Unknown, string playerCardId1 = null, string playerCardId2 = null, string effectText = "")
    {
        Id = id;
        Name = name;
        Faction = faction;
        Level = level;
        Power = power;
        Sign = sign;
        Type = type;
        Race = race;
        ArtworkPath = artworkPath;
        EffectText = effectText ?? string.Empty;
        Color = color;
        PlayerCardId1 = playerCardId1;
        PlayerCardId2 = playerCardId2;
    }
}
