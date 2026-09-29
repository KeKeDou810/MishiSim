using System;
namespace Mishi.Networking
{
    public static class RoomText
    {
        public const int NameLimit = 12, RoomLimit = 24, ChatLimit = 200;
        public static string Validate(string value, int limit, string label)
        {
            value = value?.Trim();
            if (string.IsNullOrEmpty(value) || value.Length > limit) throw new ArgumentException($"{label}需要 1～{limit} 个字符。");
            foreach (char c in value) if (char.IsControl(c) || char.IsSurrogate(c) || c == '<' || c == '>')
                throw new ArgumentException(label + "不能包含控制符、表情代理字符或尖括号。");
            return value;
        }
    }
}
