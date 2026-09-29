using System;
using System.Collections.Generic;

namespace Mishi.Battle
{
    public sealed class MatchDrawRules
    {
        public int OpeningHand { get; }
        public int DrawPerTurn { get; }
        public int TurnTimeSeconds { get; }
        public int ActionTimeRefundSeconds { get; }
        public int ResponseTimeSeconds { get; }
        private readonly HashSet<TestTurnPhase> skipped;
        public MatchDrawRules(int openingHand, int drawPerTurn, IEnumerable<TestTurnPhase> firstTurnSkip,
            int turnTimeSeconds = 120, int actionTimeRefundSeconds = 5, int responseTimeSeconds = 0)
        {
            if (openingHand < 0 || drawPerTurn < 0) throw new ArgumentOutOfRangeException();
            if (turnTimeSeconds < 1 || turnTimeSeconds > 3600 || actionTimeRefundSeconds < 0 || actionTimeRefundSeconds > turnTimeSeconds)
                throw new ArgumentOutOfRangeException(nameof(turnTimeSeconds));
            OpeningHand = openingHand; DrawPerTurn = drawPerTurn;
            TurnTimeSeconds = turnTimeSeconds; ActionTimeRefundSeconds = actionTimeRefundSeconds;
            if (responseTimeSeconds < 0 || responseTimeSeconds > 300) throw new ArgumentOutOfRangeException(nameof(responseTimeSeconds));
            ResponseTimeSeconds = responseTimeSeconds; // Zero is retained for isolated no-response combat fixtures.
            skipped = new HashSet<TestTurnPhase>(firstTurnSkip);
        }
        public bool Skip(int turn, TestTurnPhase phase) => turn == 1 && skipped.Contains(phase);
    }
}
