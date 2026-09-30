using System;
using System.Collections.Generic;
using System.Linq;

namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private sealed class ForesightCombat
        {
            public Card Attacker, Target, Revealed;
            public int AttackerGeneration, TargetGeneration, Node, Defender, Remaining, Damage = 1;
        }
        private ForesightCombat foresightCombat, declaredCombat;
        private bool CombatStillValid(ForesightCombat combat) => Winner < 0 && combat.Attacker.Zone == TestCardZone.Board &&
            !combat.Attacker.Covered && combat.Attacker.FieldGeneration == combat.AttackerGeneration &&
            (combat.Target == null ? PlayerIsExposed(combat.Defender) : combat.Target.Zone == TestCardZone.Board && !combat.Target.Covered &&
                combat.Target.FieldGeneration == combat.TargetGeneration && combat.Target.NodeId == combat.Node);
        private void BeginForesightCombat(Card attacker, Card target, int node, int defender)
        {
            foresightCombat = declaredCombat ?? new ForesightCombat { Attacker = attacker, Target = target, Node = node, Defender = defender,
                AttackerGeneration = attacker.FieldGeneration, TargetGeneration = target?.FieldGeneration ?? 0,
                Remaining = ForesightRules.MaximumChecks(effects?.Rules(attacker.DefinitionId).ForesightCount ?? 0, 1) };
            foresightCombat.Target = target; foresightCombat.TargetGeneration = target?.FieldGeneration ?? 0; foresightCombat.Node = node;
            RunSchedules("nextBattle", attacker.Owner, attacker, target);
            eventContinuations.Enqueue(ContinueForesight);
        }
        private void ContinueForesight()
        {
            var combat = foresightCombat;
            if (combat == null) return;
            if (!CombatStillValid(combat)) { EndCombat(combat, true); foresightCombat = null; return; }
            if (combat.Remaining <= 0 || !cards.Any(c => c.Owner == combat.Attacker.Owner && c.Zone == TestCardZone.Deck))
            { FinishForesightCombat(); return; }
            effectChoice = new EffectChoice { Player = combat.Attacker.Owner, IsForesightOffer = true,
                Prompt = $"是否进行未来视？本次攻击还可进行 {combat.Remaining} 次",
                ViewedCards = new[] { combat.Attacker.Copy() }, Seconds = drawRules?.ResponseTimeSeconds > 0 ? drawRules.ResponseTimeSeconds : 20 };
        }
        private void ResolveForesightOffer(bool accept)
        {
            effectChoice = null;
            if (!accept) { FinishForesightCombat(); DrainEffects(); return; }
            var combat = foresightCombat;
            if (combat == null || !CombatStillValid(combat)) { foresightCombat = null; return; }
            combat.Remaining--;
            executions.Enqueue(new EffectExecution { Source = combat.Attacker, Foresight = combat, Steps = new[] {
                new EffectInstruction { Op = EffectOp.Scry, Amount = 1, Reveal = true, Prompt = "未来视：公开牌库顶卡" }
            } });
            eventContinuations.Enqueue(() => {
                if (combat.Revealed != null && combat.Revealed.Zone == TestCardZone.Revealed) MoveEffectCard(combat.Revealed, TestCardZone.Discard, "foresight");
                combat.Revealed = null;
                // Finish movement triggers and empty-deck handling before offering the next look.
                if (drawRules != null) RecycleEmptyDeck(combat.Attacker.Owner);
                eventContinuations.Enqueue(ContinueForesight);
            });
            DrainEffects();
        }
        private void FinishForesightCombat()
        {
            var combat = foresightCombat; foresightCombat = null;
            if (combat == null) return;
            if (!CombatStillValid(combat)) { EndCombat(combat, true); return; }
            RefreshStats();
            if (combat.Target == null) { ApplyDamage(combat.Defender, Math.Max(0, combat.Damage + combat.Attacker.ContinuousContributions.Where(m => m.Stat == "damage").Sum(m => m.Amount)), true, "battle", combat.Attacker); EndCombat(combat, false); }
            else ResolveUnitAttack(combat.Attacker, combat.Target, combat.Node, out _, () => EndCombat(combat, false));
            BeginDestructionWindow();
        }
        private void EndCombat(ForesightCombat combat, bool cancelled)
        {
            declaredCombat = null;
            if (effects != null) Publish(new Events.BattleEndedEvent(EventCard(combat.Attacker, true), EventCard(combat.Target, true), combat.Defender, cancelled));
            SealAutomaticBatch();
            foreach (var card in cards) card.Modifiers.RemoveAll(m => m.BattleBound);
            destructionWards.RemoveAll(w => w.BattleBound);
            destructionReplacements.RemoveAll(r => r.BattleBound);
            grantedTriggers.RemoveAll(g => g.BattleBound);
            decisionCostModifiers.RemoveAll(m => m.BattleBound);
            selectionWards.RemoveAll(w => w.BattleBound);
            RefreshStats();
        }
        private IReadOnlyList<EffectInstruction> ForesightPlan(EffectExecution execution, Card revealed)
        {
            var context = Context(revealed, EffectEvent.Triggered, "foresight");
            if (effects.Rules(revealed.DefinitionId).ForesightMark == ForesightMark.None) return Array.Empty<EffectInstruction>();
            var custom = (effects as IForesightEffectProvider)?.BuildForesight(context);
            if (custom != null) return ValidatePlan(custom);
            switch (effects.Rules(revealed.DefinitionId).ForesightMark)
            {
                case ForesightMark.Draw: return new[] { new EffectInstruction { Op = EffectOp.Draw, Amount = 1 } };
                case ForesightMark.Heal: return new[] { new EffectInstruction { Op = EffectOp.Heal, Amount = 1 }, new EffectInstruction { Op = EffectOp.Move, Zone = TestCardZone.Exile } };
                case ForesightMark.Destroy: return new[] {
                    new EffectInstruction { Op = EffectOp.Choose, Side = "any", Prompt = "炸裂标记：选择一个场上时魔破坏" },
                    new EffectInstruction { Op = EffectOp.Destroy, Target = "selected" }
                };
                case ForesightMark.Special: throw new FormatException(revealed.DefinitionId + " 的特殊标记需要 effects.onForesight。");
                default: return Array.Empty<EffectInstruction>();
            }
        }
        private void FinishForesightScry(EffectExecution execution, Card[] viewed, bool activateMark)
        {
            if (viewed.Length == 0) return;
            var card = viewed[0]; var combat = execution.Foresight;
            combat.Revealed = card;
            Publish(new Events.ForesightRevealedEvent(EventCard(combat.Attacker, true), EventCard(card, true)));
            if (!activateMark)
            {
                AddLog(card.Owner, "不发动特效标记：" + CardLabel(card));
                return;
            }
            execution.Source = card;
            var plan = ForesightPlan(execution, card);
            execution.ResolvedMark = BindScript(plan, card.DefinitionId);
            execution.Steps = execution.Steps.Take(execution.Index).Concat(plan).Concat(execution.Steps.Skip(execution.Index)).ToArray();
        }
    }
}
