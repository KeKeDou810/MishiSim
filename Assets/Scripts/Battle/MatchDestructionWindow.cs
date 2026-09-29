using System;
using System.Collections.Generic;
using System.Linq;
namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private sealed class DestructionRequest
        {
            public readonly Guid Id = Guid.NewGuid();
            public Card Target, Cause;
            public int Generation;
            public string Reason;
            public Action Completed;
        }
        private readonly List<DestructionRequest> destructionRequests = new List<DestructionRequest>();
        private readonly Dictionary<Guid, DestructionRequest> pendingDestructions = new Dictionary<Guid, DestructionRequest>();
        private void RequestDestruction(Card target, string reason, Action completed = null)
        {
            if (target.Zone != TestCardZone.Board) { completed?.Invoke(); return; }
            if (effects == null) { DestroyPile(target, reason); completed?.Invoke(); return; }
            if (destructionRequests.Any(r => r.Target.NodeId == target.NodeId)) return;
            destructionRequests.Add(new DestructionRequest { Target = target, Generation = target.FieldGeneration, Cause = eventCause, Reason = reason, Completed = completed });
        }
        // Suspend the remaining instruction sequence, allowing optional replacement listeners to resolve before mutation.
        private void BeginDestructionWindow()
        {
            if (destructionRequests.Count == 0) return;
            var requests = destructionRequests.ToArray(); destructionRequests.Clear();
            var suspended = executions.ToArray(); executions.Clear();
            var continuations = eventContinuations.ToArray(); eventContinuations.Clear();
            foreach (var request in requests)
            {
                pendingDestructions.Add(request.Id, request);
                Publish(new Events.DestructionPendingEvent(request.Id, EventCard(request.Target, true), request.Reason, EventCard(request.Cause)));
            }
            SealAutomaticBatch();
            eventContinuations.Enqueue(() => {
                var previousObservers = eventListenersBefore; eventListenersBefore = CaptureListeners();
                try
                {
                    foreach (var request in requests)
                    {
                        pendingDestructions.Remove(request.Id);
                        if (request.Target.Zone == TestCardZone.Board && request.Target.FieldGeneration == request.Generation)
                        {
                            var previous = eventCause; eventCause = request.Cause;
                            try { DestroyPile(request.Target, request.Reason); } finally { eventCause = previous; }
                        }
                    }
                }
                finally { eventListenersBefore = previousObservers; }
                RefreshStats();
                foreach (var execution in suspended) executions.Enqueue(execution);
                // Destruction triggers resolve before battle-end/occupation continuations.
                foreach (var request in requests) if (request.Completed != null) eventContinuations.Enqueue(request.Completed);
                foreach (var continuation in continuations) eventContinuations.Enqueue(continuation);
            });
        }
        private void ChangeDestructionReason(EffectExecution execution, EffectInstruction step)
        {
            if (execution.EventData is Events.DestructionPendingEvent pending && pendingDestructions.TryGetValue(pending.RequestId, out var request))
                request.Reason = step.From;
        }
    }
}
