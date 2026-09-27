using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

// Immutable definitions and copied deck counts for one match, independent of scene UI.
// Card effects are not loaded by the current database yet; this snapshots metadata only.
public sealed class BattleCardSnapshot
{
    public IReadOnlyDictionary<string, CardDefinition> Cards { get; }
    public IReadOnlyDictionary<string, int> Deck { get; }
    public IReadOnlyList<string> PlayerCards { get; }
    public string ContractId { get; }
    public BattleCardSnapshot(IEnumerable<CardDefinition> cards, DeckModel deck)
    {
        Cards = new ReadOnlyDictionary<string, CardDefinition>(cards.ToDictionary(c => c.Id));
        Deck = new ReadOnlyDictionary<string, int>(deck.Entries.ToDictionary(e => e.Key, e => e.Value));
        PlayerCards = System.Array.AsReadOnly(deck.PlayerCards.ToArray());
        ContractId = deck.Contract?.Id;
    }
}
