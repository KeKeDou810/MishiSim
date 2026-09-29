using System.Collections.Generic;
using System.Linq;

namespace Mishi.Networking
{
    public enum RoomMemberKind { Unknown, Player, Spectator }
    // Only players occupy seats. Spectators have no application-level count limit.
    public sealed class RoomAudience<T>
    {
        private readonly Dictionary<T, int> players = new Dictionary<T, int>();
        private readonly HashSet<T> spectators = new HashSet<T>();
        public IReadOnlyDictionary<T, int> Players => players;
        public IReadOnlyCollection<T> Spectators => spectators;
        public bool AddSpectator(T connection)
        {
            if (players.ContainsKey(connection)) return false;
            return spectators.Add(connection);
        }
        public bool AddPlayer(T connection, int seat)
        {
            if (seat < 0 || seat > 1 || spectators.Contains(connection) || players.ContainsKey(connection) || players.ContainsValue(seat)) return false;
            players.Add(connection, seat); return true;
        }
        public bool AuthorizeCommand(T connection, out int player) => players.TryGetValue(connection, out player);
        public RoomMemberKind Remove(T connection)
        {
            if (spectators.Remove(connection)) return RoomMemberKind.Spectator;
            return players.Remove(connection) ? RoomMemberKind.Player : RoomMemberKind.Unknown;
        }
        public void Clear() { players.Clear(); spectators.Clear(); }
    }
}
