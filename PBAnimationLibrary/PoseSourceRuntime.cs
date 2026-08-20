using System.Collections.Generic;
using UnityEngine;

namespace PB_AnimationLibrary
{
    internal static class PoseSourceRuntime
    {
        private sealed class BoundNode
        {
            internal Transform Transform;
            internal Vector3 LocalPosition;
            internal Quaternion LocalRotation;
            internal Vector3 LocalScale;
        }

        private sealed class ActorSourceState
        {
            internal CombatEntity Actor;
            internal readonly List<BoundNode> BoundNodes =
                new List<BoundNode>();
            internal int LastMechLateUpdateFrame = -1;
        }

        private static readonly Dictionary<int, ActorSourceState> states =
            new Dictionary<int, ActorSourceState>();

        internal static bool HasSourceFor(CombatEntity actor)
        {
            ActorSourceState state;
            return TryGetState(actor, out state) &&
                   state.BoundNodes.Count > 0;
        }

        internal static bool NeedsRenderFallback(CombatEntity actor)
        {
            ActorSourceState state;
            return TryGetState(actor, out state) &&
                   state.BoundNodes.Count > 0 &&
                   state.LastMechLateUpdateFrame != Time.frameCount;
        }

        internal static bool SetSource(
            CombatEntity actor,
            PoseSnapshot snapshot,
            string name,
            out int boundNodeCount)
        {
            boundNodeCount = 0;

            int actorId;
            if (!TryGetActorId(actor, out actorId) ||
                snapshot == null)
            {
                return false;
            }

            Transform samplingRoot;
            Transform jointRoot;
            if (!VisibleMechRigResolver.TryResolve(
                    actor,
                    out samplingRoot,
                    out jointRoot))
            {
                return false;
            }

            ActorSourceState state =
                new ActorSourceState
                {
                    Actor = actor
                };

            List<PoseNodeSnapshot> nodes = snapshot.Nodes;
            for (int i = 0; i < nodes.Count; ++i)
            {
                PoseNodeSnapshot nodeSnapshot = nodes[i];
                Transform node = samplingRoot.Find(nodeSnapshot.Path);

                if (node == null)
                    continue;

                state.BoundNodes.Add(
                    new BoundNode
                    {
                        Transform = node,
                        LocalPosition = nodeSnapshot.LocalPosition,
                        LocalRotation = nodeSnapshot.LocalRotation,
                        LocalScale = nodeSnapshot.LocalScale
                    });
            }

            if (state.BoundNodes.Count == 0)
                return false;

            states[actorId] = state;
            boundNodeCount = state.BoundNodes.Count;
            return true;
        }

        internal static void Clear(CombatEntity actor)
        {
            int actorId;
            if (!TryGetActorId(actor, out actorId))
                return;

            states.Remove(actorId);
        }

        internal static void ClearAll()
        {
            states.Clear();
        }

        internal static void ApplyFromMechLateUpdate(CombatEntity actor)
        {
            ActorSourceState state;
            if (!TryGetState(actor, out state) ||
                state.BoundNodes.Count == 0)
            {
                return;
            }

            state.LastMechLateUpdateFrame = Time.frameCount;
            Apply(state);
        }

        internal static void Apply(CombatEntity actor)
        {
            ActorSourceState state;
            if (!TryGetState(actor, out state) ||
                state.BoundNodes.Count == 0)
            {
                return;
            }

            Apply(state);
        }

        private static void Apply(ActorSourceState state)
        {
            for (int i = 0; i < state.BoundNodes.Count; ++i)
            {
                BoundNode node = state.BoundNodes[i];
                if (node == null || node.Transform == null)
                    continue;

                node.Transform.localPosition = node.LocalPosition;
                node.Transform.localRotation = node.LocalRotation;
                node.Transform.localScale = node.LocalScale;
            }
        }

        private static bool TryGetState(
            CombatEntity actor,
            out ActorSourceState state)
        {
            state = null;

            int actorId;
            if (!TryGetActorId(actor, out actorId))
                return false;

            if (!states.TryGetValue(actorId, out state))
                return false;

            return state.Actor == actor;
        }

        private static bool TryGetActorId(
            CombatEntity actor,
            out int actorId)
        {
            actorId = -1;

            if (actor == null || !actor.hasId)
                return false;

            actorId = actor.id.id;
            return true;
        }
    }
}
