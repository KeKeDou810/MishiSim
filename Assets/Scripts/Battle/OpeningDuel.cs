using System;

namespace Mishi.Battle
{
    public enum OpeningStage { None, Gesture, Result, TurnOrder, Hand, Complete }

    // Host-owned simultaneous selection. Unresolved gestures are never included in a public view.
    public sealed class OpeningDuel
    {
        private readonly Random random;
        private readonly double choiceSeconds;
        private readonly int[] gestures = { -1, -1 };
        public OpeningStage Stage { get; private set; }
        public int Epoch { get; private set; }
        public int Round { get; private set; } = 1;
        public int Winner { get; private set; } = -1;
        public int FirstPlayer { get; private set; } = -1;
        public double Seconds { get; private set; }

        public OpeningDuel(double choiceSeconds, Random random)
        {
            if (choiceSeconds <= 0 || double.IsNaN(choiceSeconds) || double.IsInfinity(choiceSeconds)) throw new ArgumentOutOfRangeException(nameof(choiceSeconds));
            this.choiceSeconds = choiceSeconds;
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            Enter(OpeningStage.Gesture, choiceSeconds);
        }
        private void Enter(OpeningStage stage, double seconds) { Stage = stage; Seconds = seconds; Epoch++; }
        public bool HasChosen(int player) => player >= 0 && player < 2 && gestures[player] >= 0;
        public int VisibleGesture(int player, int viewer) => player >= 0 && player < 2 && (Stage != OpeningStage.Gesture || player == viewer) ? gestures[player] : -1;
        // 0 = rock, 1 = scissors, 2 = paper.
        public static int Resolve(int left, int right)
        {
            if (left < 0 || left > 2 || right < 0 || right > 2) throw new ArgumentOutOfRangeException();
            return left == right ? -1 : (left + 1) % 3 == right ? 0 : 1;
        }
        public bool Choose(int player, int epoch, int value)
        {
            if (player < 0 || player > 1 || epoch != Epoch) return false;
            if (Stage == OpeningStage.Gesture)
            {
                if (value < 0 || value > 2 || HasChosen(player)) return false;
                gestures[player] = value;
                if (HasChosen(0) && HasChosen(1)) Reveal();
                return true;
            }
            if (Stage != OpeningStage.TurnOrder || player != Winner || value < 0 || value > 1) return false;
            FirstPlayer = value == 0 ? Winner : 1 - Winner;
            Enter(OpeningStage.Hand, choiceSeconds);
            return true;
        }
        private void Reveal() { Winner = Resolve(gestures[0], gestures[1]); Enter(OpeningStage.Result, 2.5); }
        public bool Advance(double elapsed)
        {
            if (elapsed < 0 || double.IsNaN(elapsed) || double.IsInfinity(elapsed)) throw new ArgumentOutOfRangeException(nameof(elapsed));
            if (Stage == OpeningStage.Hand || Stage == OpeningStage.Complete) return false;
            Seconds = Math.Max(0, Seconds - elapsed);
            if (Seconds > 0) return false;
            if (Stage == OpeningStage.Gesture)
            {
                for (int p = 0; p < 2; p++) if (!HasChosen(p)) gestures[p] = random.Next(3);
                Reveal();
            }
            else if (Stage == OpeningStage.Result)
            {
                if (Winner < 0) { Round++; gestures[0] = gestures[1] = -1; Enter(OpeningStage.Gesture, choiceSeconds); }
                else Enter(OpeningStage.TurnOrder, choiceSeconds);
            }
            else if (Stage == OpeningStage.TurnOrder) Choose(Winner, Epoch, 0);
            return true;
        }
        public void Complete() { if (Stage == OpeningStage.Hand) Enter(OpeningStage.Complete, 0); }
    }
}
