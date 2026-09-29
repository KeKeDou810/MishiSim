using System;
using System.Linq;

namespace Mishi.Battle.Events
{
    // Shared by every card event: summon, play, activate, draw, move and destruction.
    public sealed class EventCardFilter
    {
        public string Type, Race, Name, NameMatch = "exact";
        public string[] Types = Array.Empty<string>();
        public void Validate()
        {
            if (NameMatch != "exact" && NameMatch != "fuzzy") throw new FormatException("nameMatch must be exact or fuzzy.");
            if (Types == null || Types.Length > 16 || Types.Any(string.IsNullOrWhiteSpace)) throw new FormatException("types must contain at most 16 non-empty card types.");
            if (!string.IsNullOrEmpty(Type) && Types.Length > 0) throw new FormatException("Use type or types, not both.");
            if ((Name?.Length ?? 0) > 128) throw new FormatException("Card name filter is too long.");
        }
        public bool Matches(EventCard card, int viewer)
        {
            if (string.IsNullOrEmpty(Type) && Types.Length == 0 && string.IsNullOrEmpty(Race) && string.IsNullOrEmpty(Name)) return true;
            if (card == null || !card.VisibleTo(viewer)) return false;
            if (!string.IsNullOrEmpty(Type) && card.Type != Type || Types.Length > 0 && !Types.Contains(card.Type)) return false;
            if (!string.IsNullOrEmpty(Race) && card.Race != Race) return false;
            return string.IsNullOrEmpty(Name) || (NameMatch == "exact" ? string.Equals(card.Name, Name, StringComparison.Ordinal)
                : (card.Name ?? "").IndexOf(Name, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
