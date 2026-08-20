using System.Collections.Generic;
using UnityEngine;

namespace PB_AnimationLibrary
{
    internal sealed class PoseOverrideCaptureNode
    {
        internal string Path;

        internal bool HasPosition;
        internal Vector3 SourcePosition;
        internal Vector3 Position;

        internal bool HasRotation;
        internal Quaternion SourceRotation;
        internal Quaternion Rotation;
    }

    internal static class PoseOverrideRuntime
    {
        private sealed class NodeOverride
        {
            internal string Path;

            internal bool HasPosition;
            internal Vector3 SourcePosition;
            internal Vector3 Position;

            internal bool HasRotation;
            internal Quaternion SourceRotation;
            internal Quaternion Rotation;
        }

        private sealed class ActorOverrideState
        {
            internal CombatEntity Actor;
            internal readonly Dictionary<string, NodeOverride> Nodes =
                new Dictionary<string, NodeOverride>();
            internal int LastMechLateUpdateFrame = -1;
        }

        private static readonly Dictionary<int, ActorOverrideState> states =
            new Dictionary<int, ActorOverrideState>();

        internal static bool HasOverridesFor(CombatEntity actor)
        {
            ActorOverrideState state;
            return TryGetState(actor, out state) &&
                   state.Nodes.Count > 0;
        }

        internal static int GetOverrideCount(CombatEntity actor)
        {
            ActorOverrideState state;
            return TryGetState(actor, out state)
                ? state.Nodes.Count
                : 0;
        }

        internal static bool NeedsRenderFallback(CombatEntity actor)
        {
            ActorOverrideState state;
            return TryGetState(actor, out state) &&
                   state.Nodes.Count > 0 &&
                   state.LastMechLateUpdateFrame != Time.frameCount;
        }

        internal static int CaptureCurrent(
            CombatEntity actor,
            List<PoseOverrideCaptureNode> output)
        {
            if (output == null)
                return 0;

            output.Clear();

            ActorOverrideState state;
            if (!TryGetState(actor, out state))
                return 0;

            foreach (NodeOverride entry in state.Nodes.Values)
            {
                output.Add(
                    new PoseOverrideCaptureNode
                    {
                        Path = entry.Path,
                        HasPosition = entry.HasPosition,
                        SourcePosition = entry.SourcePosition,
                        Position = entry.Position,
                        HasRotation = entry.HasRotation,
                        SourceRotation = entry.SourceRotation,
                        Rotation = entry.Rotation
                    });
            }

            output.Sort(CompareCaptureNodesByPath);
            return output.Count;
        }

        internal static void DiscardAll(CombatEntity actor)
        {
            int actorId;
            if (!TryGetActorId(actor, out actorId))
                return;

            states.Remove(actorId);
        }

        internal static void SetPosition(
            CombatEntity actor,
            string path,
            Vector3 sourcePosition,
            Vector3 position)
        {
            NodeOverride entry = GetOrCreate(actor, path);
            if (entry == null)
                return;

            if (!entry.HasPosition)
                entry.SourcePosition = sourcePosition;

            entry.HasPosition = true;
            entry.Position = position;
        }

        internal static void SetRotation(
            CombatEntity actor,
            string path,
            Quaternion sourceRotation,
            Quaternion rotation)
        {
            NodeOverride entry = GetOrCreate(actor, path);
            if (entry == null)
                return;

            if (!entry.HasRotation)
                entry.SourceRotation = sourceRotation;

            entry.HasRotation = true;
            entry.Rotation = rotation;
        }

        internal static bool TryGetPosition(
            CombatEntity actor,
            string path,
            out Vector3 position)
        {
            position = Vector3.zero;

            NodeOverride entry;
            if (!TryGetEntry(actor, path, out entry) ||
                !entry.HasPosition)
            {
                return false;
            }

            position = entry.Position;
            return true;
        }

        internal static bool TryGetRotation(
            CombatEntity actor,
            string path,
            out Quaternion rotation)
        {
            rotation = Quaternion.identity;

            NodeOverride entry;
            if (!TryGetEntry(actor, path, out entry) ||
                !entry.HasRotation)
            {
                return false;
            }

            rotation = entry.Rotation;
            return true;
        }

        internal static void ResetPosition(
            CombatEntity actor,
            string path)
        {
            ActorOverrideState state;
            NodeOverride entry;
            if (!TryGetState(actor, out state) ||
                !state.Nodes.TryGetValue(path, out entry) ||
                !entry.HasPosition)
            {
                return;
            }

            RestorePosition(actor, entry);
            entry.HasPosition = false;
            RemoveIfEmpty(actor, state, path, entry);
        }

        internal static void ResetRotation(
            CombatEntity actor,
            string path)
        {
            ActorOverrideState state;
            NodeOverride entry;
            if (!TryGetState(actor, out state) ||
                !state.Nodes.TryGetValue(path, out entry) ||
                !entry.HasRotation)
            {
                return;
            }

            RestoreRotation(actor, entry);
            entry.HasRotation = false;
            RemoveIfEmpty(actor, state, path, entry);
        }

        internal static void ResetNode(
            CombatEntity actor,
            string path)
        {
            ActorOverrideState state;
            NodeOverride entry;
            if (!TryGetState(actor, out state) ||
                !state.Nodes.TryGetValue(path, out entry))
            {
                return;
            }

            RestoreNode(actor, entry);
            state.Nodes.Remove(path);
            RemoveStateIfEmpty(actor, state);
        }

        internal static int ResetBranch(
            CombatEntity actor,
            string branchPath)
        {
            ActorOverrideState state;
            if (!TryGetState(actor, out state) ||
                string.IsNullOrEmpty(branchPath))
            {
                return 0;
            }

            List<string> paths = new List<string>();
            string descendantPrefix = branchPath + "/";

            foreach (KeyValuePair<string, NodeOverride> pair in state.Nodes)
            {
                if (pair.Key == branchPath ||
                    pair.Key.StartsWith(descendantPrefix))
                {
                    paths.Add(pair.Key);
                }
            }

            for (int i = 0; i < paths.Count; ++i)
            {
                string path = paths[i];
                NodeOverride entry;
                if (!state.Nodes.TryGetValue(path, out entry))
                    continue;

                RestoreNode(actor, entry);
                state.Nodes.Remove(path);
            }

            RemoveStateIfEmpty(actor, state);
            return paths.Count;
        }

        internal static void ClearAll(CombatEntity actor)
        {
            ActorOverrideState state;
            if (!TryGetState(actor, out state))
                return;

            foreach (NodeOverride entry in state.Nodes.Values)
                RestoreNode(actor, entry);

            int actorId;
            if (TryGetActorId(actor, out actorId))
                states.Remove(actorId);
        }

        internal static void ClearAllWithoutRestore()
        {
            states.Clear();
        }

        internal static void ApplyFromMechLateUpdate(CombatEntity actor)
        {
            ActorOverrideState state;
            if (!TryGetState(actor, out state) ||
                state.Nodes.Count == 0)
            {
                return;
            }

            state.LastMechLateUpdateFrame = Time.frameCount;
            Apply(actor, state);
        }

        internal static void Apply(CombatEntity actor)
        {
            ActorOverrideState state;
            if (!TryGetState(actor, out state) ||
                state.Nodes.Count == 0)
            {
                return;
            }

            Apply(actor, state);
        }

        private static void Apply(
            CombatEntity actor,
            ActorOverrideState state)
        {
            Transform samplingRoot;
            Transform jointRoot;
            if (!VisibleMechRigResolver.TryResolve(
                    actor,
                    out samplingRoot,
                    out jointRoot))
            {
                return;
            }

            foreach (NodeOverride entry in state.Nodes.Values)
            {
                Transform node = samplingRoot.Find(entry.Path);
                if (node == null)
                    continue;

                if (entry.HasPosition)
                    node.localPosition = entry.Position;

                if (entry.HasRotation)
                    node.localRotation = entry.Rotation;
            }
        }

        private static NodeOverride GetOrCreate(
            CombatEntity actor,
            string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            ActorOverrideState state = GetOrCreateState(actor);
            if (state == null)
                return null;

            NodeOverride entry;
            if (!state.Nodes.TryGetValue(path, out entry))
            {
                entry =
                    new NodeOverride
                    {
                        Path = path
                    };

                state.Nodes.Add(path, entry);
            }

            return entry;
        }

        private static ActorOverrideState GetOrCreateState(
            CombatEntity actor)
        {
            int actorId;
            if (!TryGetActorId(actor, out actorId))
                return null;

            ActorOverrideState state;
            if (states.TryGetValue(actorId, out state) &&
                state.Actor == actor)
            {
                return state;
            }

            state =
                new ActorOverrideState
                {
                    Actor = actor
                };

            states[actorId] = state;
            return state;
        }

        private static int CompareCaptureNodesByPath(
            PoseOverrideCaptureNode left,
            PoseOverrideCaptureNode right)
        {
            string leftPath =
                left != null ? left.Path : string.Empty;

            string rightPath =
                right != null ? right.Path : string.Empty;

            return string.CompareOrdinal(
                leftPath,
                rightPath);
        }

        private static bool TryGetEntry(
            CombatEntity actor,
            string path,
            out NodeOverride entry)
        {
            entry = null;

            if (string.IsNullOrEmpty(path))
                return false;

            ActorOverrideState state;
            if (!TryGetState(actor, out state))
                return false;

            return state.Nodes.TryGetValue(path, out entry);
        }

        private static bool TryGetState(
            CombatEntity actor,
            out ActorOverrideState state)
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

        private static void RestoreNode(
            CombatEntity actor,
            NodeOverride entry)
        {
            Transform node;
            if (!TryResolveNode(actor, entry.Path, out node))
                return;

            if (entry.HasPosition)
                node.localPosition = entry.SourcePosition;

            if (entry.HasRotation)
                node.localRotation = entry.SourceRotation;
        }

        private static void RestorePosition(
            CombatEntity actor,
            NodeOverride entry)
        {
            Transform node;
            if (TryResolveNode(actor, entry.Path, out node))
                node.localPosition = entry.SourcePosition;
        }

        private static void RestoreRotation(
            CombatEntity actor,
            NodeOverride entry)
        {
            Transform node;
            if (TryResolveNode(actor, entry.Path, out node))
                node.localRotation = entry.SourceRotation;
        }

        private static bool TryResolveNode(
            CombatEntity actor,
            string path,
            out Transform node)
        {
            node = null;

            Transform samplingRoot;
            Transform jointRoot;
            if (!VisibleMechRigResolver.TryResolve(
                    actor,
                    out samplingRoot,
                    out jointRoot))
            {
                return false;
            }

            node = samplingRoot.Find(path);
            return node != null;
        }

        private static void RemoveIfEmpty(
            CombatEntity actor,
            ActorOverrideState state,
            string path,
            NodeOverride entry)
        {
            if (entry.HasPosition || entry.HasRotation)
                return;

            state.Nodes.Remove(path);
            RemoveStateIfEmpty(actor, state);
        }

        private static void RemoveStateIfEmpty(
            CombatEntity actor,
            ActorOverrideState state)
        {
            if (state.Nodes.Count != 0)
                return;

            int actorId;
            if (TryGetActorId(actor, out actorId))
                states.Remove(actorId);
        }
    }
}
