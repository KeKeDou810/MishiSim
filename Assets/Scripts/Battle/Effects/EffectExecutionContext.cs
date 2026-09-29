using System.Collections.Generic;
namespace Mishi.Battle.Effects
{
    // Host-owned mutation port. No Unity objects, transport, or global service access.
    public abstract class EffectExecutionContext
    {
        public abstract NetworkTestMatch.Card Source { get; }
        public abstract IReadOnlyList<NetworkTestMatch.Card> Targets { get; }
        public abstract int Player { get; }
        public abstract int Turn { get; }
        public virtual void Pay(EffectInstruction step) => throw new System.NotSupportedException();
        public virtual void ChangeDestructionReason(EffectInstruction step) => throw new System.NotSupportedException();
        public virtual void BlockName(EffectInstruction step) => throw new System.NotSupportedException();
        public virtual void ProtectSelection(EffectInstruction step) => throw new System.NotSupportedException();
        public abstract void WriteValue(EffectInstruction instruction, bool add);
        public abstract void ClearValue(EffectInstruction instruction);
        public abstract void DeclareCardName(EffectInstruction instruction);
        public abstract void RememberCards(string key, IReadOnlyList<NetworkTestMatch.Card> cards);
        public abstract void ReturnToDeck(IReadOnlyList<NetworkTestMatch.Card> cards, string position);
        public abstract void Scry(EffectInstruction instruction);
        public abstract void Choose(EffectInstruction instruction);
        public abstract void Draw(int player, int amount);
        public abstract void Damage(int player, int amount, bool moveCost);
        public abstract void Heal(int player, int amount, bool moveCost, int minimumDamage);
        public abstract void AdjustCost(int player, int amount, int maximumCost);
        public abstract void Destroy(NetworkTestMatch.Card card);
        public abstract void Move(NetworkTestMatch.Card card, TestCardZone destination);
        public abstract void PlaceOffField(IReadOnlyList<NetworkTestMatch.Card> cards);
        public abstract void Spawn(EffectInstruction instruction);
        public virtual void Summon(IReadOnlyList<NetworkTestMatch.Card> cards) => throw new System.NotSupportedException();
        public virtual void Summon(IReadOnlyList<NetworkTestMatch.Card> cards, string placement) => Summon(cards);
        public virtual void Schedule(EffectInstruction instruction) => throw new System.NotSupportedException();
        public virtual void AttachPlayers() => throw new System.NotSupportedException();
        public virtual void RollDice(EffectInstruction step) => throw new System.NotSupportedException();
        public virtual void Continue(EffectInstruction step) => throw new System.NotSupportedException();
        public virtual void QueryCards(EffectInstruction step) => throw new System.NotSupportedException();
        public virtual void InvokeDecision(EffectInstruction step) => throw new System.NotSupportedException();
        public virtual void SpawnBoard(EffectInstruction step) => throw new System.NotSupportedException();
        public virtual void RemoveToken(EffectInstruction step) => throw new System.NotSupportedException();
        public virtual void PreventDestruction(EffectInstruction step) => throw new System.NotSupportedException();
        public virtual void RedirectAttack(EffectInstruction step) => throw new System.NotSupportedException();
        public virtual void ChooseHiddenHand(EffectInstruction step) => throw new System.NotSupportedException();
        public virtual void GrantTrigger(EffectInstruction step) => throw new System.NotSupportedException();
        public virtual void CopyForesight(EffectInstruction step) => throw new System.NotSupportedException();
        public virtual void ReplaceDestruction(EffectInstruction step) => throw new System.NotSupportedException();
        public virtual void ModifyDecisionCost(EffectInstruction step) => throw new System.NotSupportedException();
        public virtual void AttachUnder(EffectInstruction step) => throw new System.NotSupportedException();
        public virtual void Shuffle(int player) => throw new System.NotSupportedException();
        public virtual void Reveal(NetworkTestMatch.Card card) => throw new System.NotSupportedException();
        public virtual void ModifyRevealTime(int amount) => throw new System.NotSupportedException();
        public virtual void Ready(NetworkTestMatch.Card card) => card.Tapped = false;
        public virtual void SuppressEffects(NetworkTestMatch.Card card) => throw new System.NotSupportedException();
        public virtual void ProtectFromEnemyUnitEffects(NetworkTestMatch.Card card) => throw new System.NotSupportedException();
        public virtual void ModifyBattleDamage(int amount, IReadOnlyList<NetworkTestMatch.Card> targets) => ModifyBattleDamage(amount);
        public virtual void ModifyBattleDamage(int amount) => throw new System.NotSupportedException();
    }
}
