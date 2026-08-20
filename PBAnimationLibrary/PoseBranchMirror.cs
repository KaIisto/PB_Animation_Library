using System;
using System.Collections.Generic;
using UnityEngine;

namespace PB_AnimationLibrary
{
    internal sealed class PoseBranchMirrorResult
    {
        internal int SourceNodeCount;
        internal int PoseNodeCount;
        internal int PairedNodeCount;
        internal int SkippedNodeCount;
        internal int ExcludedNodeCount;
        internal float MaxPositionError;
        internal float MaxRotationErrorDegrees;
    }

    internal static class PoseBranchMirror
    {
        private sealed class NodePair
        {
            internal PoseNodeSnapshot Source;
            internal PoseNodeSnapshot Target;
        }

        internal static bool IsPoseBranchRoot(
            PoseAuthoringBoneSet authoringBones,
            PoseNodeSnapshot node,
            bool includeFingerBones)
        {
            return authoringBones != null &&
                   node != null &&
                   authoringBones.CanEdit(
                       node.Path,
                       includeFingerBones);
        }

        internal static bool Mirror(
            CombatEntity actor,
            PoseSnapshot sourcePose,
            PoseAuthoringBoneSet authoringBones,
            bool includeFingerBones,
            PoseNodeSnapshot sourceRoot,
            PoseNodeSnapshot targetRoot,
            out PoseBranchMirrorResult result)
        {
            result = new PoseBranchMirrorResult();

            if (actor == null ||
                sourcePose == null ||
                authoringBones == null ||
                sourceRoot == null ||
                targetRoot == null ||
                !IsPoseBranchRoot(
                    authoringBones,
                    sourceRoot,
                    includeFingerBones) ||
                !IsPoseBranchRoot(
                    authoringBones,
                    targetRoot,
                    includeFingerBones))
            {
                return false;
            }

            List<NodePair> pairs = CollectPairs(
                sourcePose,
                authoringBones,
                includeFingerBones,
                sourceRoot,
                targetRoot,
                result);

            if (pairs.Count == 0)
                return false;

            for (int i = 0; i < pairs.Count; ++i)
            {
                NodePair pair = pairs[i];

                RootPose sourceCurrent;
                if (!TryGetRootPose(
                        actor,
                        sourcePose,
                        pair.Source.Path,
                        true,
                        out sourceCurrent))
                {
                    ++result.SkippedNodeCount;
                    continue;
                }

                Vector3 desiredTargetRootPosition =
                    MirrorRootSpacePosition(sourceCurrent.Position);

                Quaternion desiredTargetRootRotation =
                    MirrorRootSpaceRotation(sourceCurrent.Rotation);

                RootPose targetParent = RootPose.Identity;
                if (!string.IsNullOrEmpty(pair.Target.ParentPath) &&
                    !TryGetRootPose(
                        actor,
                        sourcePose,
                        pair.Target.ParentPath,
                        true,
                        out targetParent))
                {
                    ++result.SkippedNodeCount;
                    continue;
                }

                Vector3 targetLocalPosition =
                    ToLocalPosition(
                        targetParent,
                        desiredTargetRootPosition);

                Quaternion targetLocalRotation =
                    Normalize(
                        Quaternion.Inverse(targetParent.Rotation) *
                        desiredTargetRootRotation);

                PoseOverrideRuntime.SetPosition(
                    actor,
                    pair.Target.Path,
                    pair.Target.LocalPosition,
                    targetLocalPosition);

                PoseOverrideRuntime.SetRotation(
                    actor,
                    pair.Target.Path,
                    pair.Target.LocalRotation,
                    targetLocalRotation);

                RootPose appliedTarget;
                if (TryGetRootPose(
                        actor,
                        sourcePose,
                        pair.Target.Path,
                        true,
                        out appliedTarget))
                {
                    result.MaxPositionError = Mathf.Max(
                        result.MaxPositionError,
                        Vector3.Distance(
                            desiredTargetRootPosition,
                            appliedTarget.Position));

                    result.MaxRotationErrorDegrees = Mathf.Max(
                        result.MaxRotationErrorDegrees,
                        Quaternion.Angle(
                            desiredTargetRootRotation,
                            appliedTarget.Rotation));
                }
            }

            return result.PairedNodeCount > 0;
        }

        private static List<NodePair> CollectPairs(
            PoseSnapshot sourcePose,
            PoseAuthoringBoneSet authoringBones,
            bool includeFingerBones,
            PoseNodeSnapshot sourceRoot,
            PoseNodeSnapshot targetRoot,
            PoseBranchMirrorResult result)
        {
            List<NodePair> pairs = new List<NodePair>();
            string sourcePrefix = sourceRoot.Path + "/";
            string targetPrefix = targetRoot.Path + "/";

            List<PoseNodeSnapshot> nodes = sourcePose.Nodes;
            for (int i = 0; i < nodes.Count; ++i)
            {
                PoseNodeSnapshot sourceNode = nodes[i];

                if (sourceNode.Path != sourceRoot.Path &&
                    !sourceNode.Path.StartsWith(
                        sourcePrefix,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                ++result.SourceNodeCount;

                if (!authoringBones.CanEdit(
                        sourceNode.Path,
                        includeFingerBones))
                {
                    ++result.ExcludedNodeCount;
                    continue;
                }

                ++result.PoseNodeCount;

                PoseNodeSnapshot targetNode;
                if (!PoseCounterpartResolver.TryResolve(
                        sourcePose,
                        sourceNode.Path,
                        out targetNode))
                {
                    ++result.SkippedNodeCount;
                    continue;
                }

                if ((targetNode.Path != targetRoot.Path &&
                     !targetNode.Path.StartsWith(
                         targetPrefix,
                         StringComparison.Ordinal)) ||
                    !authoringBones.CanEdit(
                        targetNode.Path,
                        includeFingerBones))
                {
                    ++result.SkippedNodeCount;
                    continue;
                }

                pairs.Add(
                    new NodePair
                    {
                        Source = sourceNode,
                        Target = targetNode
                    });

                ++result.PairedNodeCount;
            }

            return pairs;
        }

        private static bool TryGetRootPose(
            CombatEntity actor,
            PoseSnapshot sourcePose,
            string path,
            bool includeOverrides,
            out RootPose rootPose)
        {
            rootPose = RootPose.Identity;

            PoseNodeSnapshot node;
            if (!sourcePose.TryGetNode(path, out node))
                return false;

            // joint_root 자체의 local transform은 mirror 좌표계 밖에 있으므로 identity로 취급
            if (string.IsNullOrEmpty(node.ParentPath))
            {
                rootPose = RootPose.Identity;
                return true;
            }

            RootPose parent;
            if (!TryGetRootPose(
                    actor,
                    sourcePose,
                    node.ParentPath,
                    includeOverrides,
                    out parent))
            {
                return false;
            }

            Vector3 localPosition = node.LocalPosition;
            Quaternion localRotation = node.LocalRotation;

            if (includeOverrides)
            {
                Vector3 overridePosition;
                if (PoseOverrideRuntime.TryGetPosition(
                        actor,
                        path,
                        out overridePosition))
                {
                    localPosition = overridePosition;
                }

                Quaternion overrideRotation;
                if (PoseOverrideRuntime.TryGetRotation(
                        actor,
                        path,
                        out overrideRotation))
                {
                    localRotation = overrideRotation;
                }
            }

            Vector3 scaledLocalPosition =
                Vector3.Scale(
                    parent.Scale,
                    localPosition);

            rootPose.Position =
                parent.Position +
                parent.Rotation * scaledLocalPosition;

            rootPose.Rotation =
                Normalize(
                    parent.Rotation * localRotation);

            rootPose.Scale =
                Vector3.Scale(
                    parent.Scale,
                    node.LocalScale);

            return true;
        }

        private static Vector3 MirrorRootSpacePosition(Vector3 position)
        {
            return new Vector3(
                -position.x,
                position.y,
                position.z);
        }

        private static Quaternion MirrorRootSpaceRotation(
            Quaternion rotation)
        {
            // joint_root local X=0 평면의 orientation 반사는 Y/Z 회전축 성분을 반전
            return Normalize(
                new Quaternion(
                    rotation.x,
                    -rotation.y,
                    -rotation.z,
                    rotation.w));
        }

        private static Vector3 ToLocalPosition(
            RootPose parent,
            Vector3 rootPosition)
        {
            Vector3 unrotated =
                Quaternion.Inverse(parent.Rotation) *
                (rootPosition - parent.Position);

            return new Vector3(
                SafeDivide(unrotated.x, parent.Scale.x),
                SafeDivide(unrotated.y, parent.Scale.y),
                SafeDivide(unrotated.z, parent.Scale.z));
        }

        private static float SafeDivide(
            float value,
            float divisor)
        {
            return Mathf.Abs(divisor) > 0.000001f
                ? value / divisor
                : 0f;
        }

        private static Quaternion Normalize(Quaternion rotation)
        {
            float magnitude = Mathf.Sqrt(
                rotation.x * rotation.x +
                rotation.y * rotation.y +
                rotation.z * rotation.z +
                rotation.w * rotation.w);

            if (magnitude <= 0.000001f)
                return Quaternion.identity;

            float inverse = 1f / magnitude;

            return new Quaternion(
                rotation.x * inverse,
                rotation.y * inverse,
                rotation.z * inverse,
                rotation.w * inverse);
        }

        private struct RootPose
        {
            internal Vector3 Position;
            internal Quaternion Rotation;
            internal Vector3 Scale;

            internal static RootPose Identity
            {
                get
                {
                    return new RootPose
                    {
                        Position = Vector3.zero,
                        Rotation = Quaternion.identity,
                        Scale = Vector3.one
                    };
                }
            }
        }
    }
}
