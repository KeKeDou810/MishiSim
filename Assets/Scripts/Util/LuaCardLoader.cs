using System;
using MoonSharp.Interpreter;

public static class LuaCardLoader
{
    public static CardDefinition Load(string source)
    {
        var script = new Script(CoreModules.Preset_HardSandbox);
        DynValue result = script.DoString(source);

        if (result.Type != DataType.Table)
            throw new FormatException("卡片 Lua 必须返回一个 table。");

        Table table = result.Table;
        string type = ReadString(table, "type");
        bool isPlayer = type == "玩家卡";
        bool hasNoCombatStats = isPlayer || type == "决策卡";

        return new CardDefinition(
            ReadString(table, "id"),
            ReadString(table, "name"),
            ReadString(table, "faction"),
            ReadInt(table, "level"),
            hasNoCombatStats ? 0 : ReadInt(table, "power"),
            isPlayer ? ReadOptionalString(table, "sign") : ReadString(table, "sign"),
            type,
            hasNoCombatStats ? string.Empty : ReadString(table, "race"),
            ReadString(table, "artworkPath"),
            ReadColor(table),
            ReadPlayerId(table, 1),
            ReadPlayerId(table, 2),
            ReadOptionalString(table, "effectText")
        );
    }

    private static string ReadOptionalString(Table table, string field)
    {
        DynValue value = table.Get(field);
        if (value.Type == DataType.Nil) return string.Empty;
        if (value.Type != DataType.String)
            throw new FormatException($"卡片字段 '{field}' 必须是 string。");
        return value.String;
    }

    private static CardColor ReadColor(Table table)
    {
        DynValue value = table.Get("color");
        if (value.Type == DataType.Nil) return CardColor.Unknown;
        if (value.Type != DataType.String)
            throw new FormatException("color 必须是 string。");
        switch (value.String)
        {
            case "blue": case "蓝": return CardColor.Blue;
            case "green": case "绿": return CardColor.Green;
            case "red": case "红": return CardColor.Red;
            case "white": case "白": return CardColor.White;
            case "black": case "黑": return CardColor.Black;
            case "generic": case "泛用": return CardColor.Generic;
            default: throw new FormatException("color 必须是 blue/green/red/white/black/generic。");
        }
    }

    private static string ReadPlayerId(Table table, int index)
    {
        DynValue players = table.Get("playerCards");
        if (players.Type == DataType.Nil) return null;
        if (players.Type != DataType.Table || players.Table.Length != 2)
            throw new FormatException("playerCards 必须包含两个玩家卡 ID。");
        DynValue id = players.Table.Get(index);
        if (id.Type != DataType.String || string.IsNullOrWhiteSpace(id.String))
            throw new FormatException("玩家卡 ID 必须是非空 string。");
        return id.String;
    }

    private static string ReadString(Table table, string field)
    {
        DynValue value = table.Get(field);

        if (value.Type != DataType.String ||
            string.IsNullOrWhiteSpace(value.String))
        {
            throw new FormatException(
                $"卡片字段 '{field}' 必须是非空 string。");
        }

        return value.String;
    }

    private static int ReadInt(Table table, string field)
    {
        DynValue value = table.Get(field);
        double number = value.Number;

        if (value.Type != DataType.Number ||
            double.IsNaN(number) ||
            double.IsInfinity(number) ||
            number < int.MinValue ||
            number > int.MaxValue ||
            number != Math.Truncate(number))
        {
            throw new FormatException(
                $"卡片字段 '{field}' 必须是 int 范围内的整数。");
        }

        return (int)number;
    }
}
