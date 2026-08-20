using System.Collections.Generic;
using UnityEngine;

namespace PB_AnimationLibrary
{
    internal sealed class PoseNodeSnapshot
    {
        internal string Name;
        internal string Path;
        internal string ParentPath;
        internal uint PathHash;
        internal Vector3 LocalPosition;
        internal Quaternion LocalRotation;
        internal Vector3 LocalScale;
    }

    internal sealed class PoseSnapshot
    {
        private readonly List<PoseNodeSnapshot> nodes;
        private readonly Dictionary<string, PoseNodeSnapshot> nodesByPath;

        internal PoseSnapshot(
            List<PoseNodeSnapshot> nodes,
            Dictionary<string, PoseNodeSnapshot> nodesByPath)
        {
            this.nodes = nodes;
            this.nodesByPath = nodesByPath;
        }

        internal List<PoseNodeSnapshot> Nodes
        {
            get { return nodes; }
        }

        internal bool TryGetNode(string path, out PoseNodeSnapshot node)
        {
            return nodesByPath.TryGetValue(path, out node);
        }
    }

    internal static class PoseSnapshotApply
    {
        internal static bool TryApply(
            CombatEntity actor,
            PoseSnapshot snapshot,
            out int appliedNodeCount)
        {
            appliedNodeCount = 0;

            if (actor == null || snapshot == null)
                return false;

            Transform samplingRoot;
            Transform jointRoot;
            if (!VisibleMechRigResolver.TryResolve(
                    actor,
                    out samplingRoot,
                    out jointRoot))
            {
                return false;
            }

            List<PoseNodeSnapshot> nodes = snapshot.Nodes;
            for (int i = 0; i < nodes.Count; ++i)
            {
                PoseNodeSnapshot nodeSnapshot = nodes[i];
                Transform node = samplingRoot.Find(nodeSnapshot.Path);

                if (node == null)
                    continue;

                node.localPosition = nodeSnapshot.LocalPosition;
                node.localRotation = nodeSnapshot.LocalRotation;
                node.localScale = nodeSnapshot.LocalScale;
                ++appliedNodeCount;
            }

            return appliedNodeCount > 0;
        }
    }

    internal static class PoseSnapshotCapture
    {
        internal static bool TryCapture(
            CombatEntity actor,
            out PoseSnapshot snapshot)
        {
            snapshot = null;

            Transform samplingRoot;
            Transform jointRoot;
            if (!VisibleMechRigResolver.TryResolve(
                    actor,
                    out samplingRoot,
                    out jointRoot))
            {
                return false;
            }

            List<PoseNodeSnapshot> nodes =
                new List<PoseNodeSnapshot>();

            Dictionary<string, PoseNodeSnapshot> nodesByPath =
                new Dictionary<string, PoseNodeSnapshot>();

            CaptureRecursive(
                jointRoot,
                jointRoot.name,
                string.Empty,
                nodes,
                nodesByPath);

            snapshot = new PoseSnapshot(nodes, nodesByPath);
            return true;
        }

        private static void CaptureRecursive(
            Transform node,
            string path,
            string parentPath,
            List<PoseNodeSnapshot> nodes,
            Dictionary<string, PoseNodeSnapshot> nodesByPath)
        {
            PoseNodeSnapshot snapshot = new PoseNodeSnapshot
            {
                Name = node.name,
                Path = path,
                ParentPath = parentPath,
                PathHash = RigPathUtility.ComputeCrc32(path),
                LocalPosition = node.localPosition,
                LocalRotation = node.localRotation,
                LocalScale = node.localScale
            };

            nodes.Add(snapshot);
            nodesByPath[path] = snapshot;

            for (int i = 0; i < node.childCount; ++i)
            {
                Transform child = node.GetChild(i);
                string childPath = path + "/" + child.name;

                CaptureRecursive(
                    child,
                    childPath,
                    path,
                    nodes,
                    nodesByPath);
            }
        }
    }
}
