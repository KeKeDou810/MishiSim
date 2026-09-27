using System;
using System.Collections.Generic;

namespace Mishi.Battle
{
    public sealed class MatchDrawRules
    {
        public int OpeningHand { get; }
        public int DrawPerTurn { get; }
        private readonly HashSet<TestTurnPhase> skipped;
        public MatchDrawRules(int openingHand, int drawPerTurn, IEnumerable<TestTurnPhase> firstTurnSkip)
        {
            if (openingHand < 0 || drawPerTurn < 0) throw new ArgumentOutOfRangeException();
            OpeningHand = openingHand; DrawPerTurn = drawPerTurn;
            skipped = new HashSet<TestTurnPhase>(firstTurnSkip);
        }
        public bool Skip(int turn, TestTurnPhase phase) => turn == 1 && skipped.Contains(phase);
    }
}
