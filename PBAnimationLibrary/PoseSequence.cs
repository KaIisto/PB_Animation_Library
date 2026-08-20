using System;
using System.Collections.Generic;
using UnityEngine;

namespace PB_AnimationLibrary
{
    internal sealed class PoseSequenceNodeDelta
    {
        internal string Path;
        internal uint PathHash;

        internal bool HasPosition;
        internal Vector3 PositionDelta;

        internal bool HasRotation;
        internal Quaternion RotationDelta;
    }

    internal sealed class PoseSequenceKeyframe
    {
        private readonly Dictionary<string, PoseSequenceNodeDelta> nodesByPath =
            new Dictionary<string, PoseSequenceNodeDelta>();

        internal float Time;

        internal int NodeCount
        {
            get { return nodesByPath.Count; }
        }

        internal IEnumerable<PoseSequenceNodeDelta> Nodes
        {
            get { return nodesByPath.Values; }
        }

        internal void Add(PoseSequenceNodeDelta node)
        {
            if (node == null ||
                string.IsNullOrEmpty(node.Path))
            {
                return;
            }

            nodesByPath[node.Path] = node;
        }

        internal bool TryGetNode(
            string path,
            out PoseSequenceNodeDelta node)
        {
            return nodesByPath.TryGetValue(
                path,
                out node);
        }
    }

    internal sealed class PoseSequence
    {
        private const float TimeEqualityTolerance = 0.0001f;

        private readonly List<PoseSequenceKeyframe> keyframes =
            new List<PoseSequenceKeyframe>();

        internal string Name =
            "pbalib_pose_sequence";

        internal float FrameRate = 30f;
        internal float Length = 1f;
        internal PoseSequenceTrackScope TrackScope =
            PoseSequenceTrackScope.EditedNodes;

        internal List<PoseSequenceKeyframe> Keyframes
        {
            get { return keyframes; }
        }

        internal int AddOrReplace(
            PoseSequenceKeyframe keyframe,
            out bool replaced)
        {
            replaced = false;

            if (keyframe == null)
                return -1;

            keyframe.Time =
                Mathf.Clamp(
                    keyframe.Time,
                    0f,
                    Mathf.Max(0.01f, Length));

            for (int i = 0; i < keyframes.Count; ++i)
            {
                if (Mathf.Abs(
                        keyframes[i].Time -
                        keyframe.Time) >
                    TimeEqualityTolerance)
                {
                    continue;
                }

                keyframes[i] = keyframe;
                replaced = true;
                return i;
            }

            keyframes.Add(keyframe);
            keyframes.Sort(CompareKeyframesByTime);

            return keyframes.IndexOf(keyframe);
        }

        internal bool RemoveAt(int index)
        {
            if (index < 0 ||
                index >= keyframes.Count)
            {
                return false;
            }

            keyframes.RemoveAt(index);
            return true;
        }

        internal void Clear()
        {
            keyframes.Clear();
        }

        private static int CompareKeyframesByTime(
            PoseSequenceKeyframe left,
            PoseSequenceKeyframe right)
        {
            float leftTime =
                left != null ? left.Time : 0f;

            float rightTime =
                right != null ? right.Time : 0f;

            return leftTime.CompareTo(
                rightTime);
        }
    }

    internal sealed class PoseSequenceSampleResult
    {
        internal int FromIndex = -1;
        internal int ToIndex = -1;
        internal float FromTime;
        internal float ToTime;
        internal float Factor;
        internal int AppliedNodeCount;
    }

    internal static class PoseSequenceCapture
    {
        internal static bool TryCapture(
            CombatEntity actor,
            PoseSnapshot sourcePose,
            PoseAuthoringBoneSet authoringBones,
            float time,
            out PoseSequenceKeyframe keyframe)
        {
            keyframe = null;

            if (actor == null ||
                sourcePose == null ||
                authoringBones == null)
            {
                return false;
            }

            List<PoseOverrideCaptureNode> overrides =
                new List<PoseOverrideCaptureNode>();

            PoseOverrideRuntime.CaptureCurrent(
                actor,
                overrides);

            PoseSequenceKeyframe captured =
                new PoseSequenceKeyframe
                {
                    Time = time
                };

            for (int i = 0; i < overrides.Count; ++i)
            {
                PoseOverrideCaptureNode entry =
                    overrides[i];

                if (entry == null ||
                    string.IsNullOrEmpty(entry.Path) ||
                    !authoringBones.Contains(
                        entry.Path))
                {
                    continue;
                }

                PoseNodeSnapshot sourceNode;
                if (!sourcePose.TryGetNode(
                        entry.Path,
                        out sourceNode))
                {
                    continue;
                }

                PoseSequenceNodeDelta delta =
                    new PoseSequenceNodeDelta
                    {
                        Path = entry.Path,
                        PathHash = sourceNode.PathHash
                    };

                if (entry.HasPosition)
                {
                    delta.HasPosition = true;
                    delta.PositionDelta =
                        entry.Position -
                        entry.SourcePosition;
                }

                if (entry.HasRotation)
                {
                    delta.HasRotation = true;
                    delta.RotationDelta =
                        Normalize(
                            Quaternion.Inverse(
                                entry.SourceRotation) *
                            entry.Rotation);
                }

                if (delta.HasPosition ||
                    delta.HasRotation)
                {
                    captured.Add(delta);
                }
            }

            keyframe = captured;
            return true;
        }

        private static Quaternion Normalize(
            Quaternion rotation)
        {
            float magnitude =
                Mathf.Sqrt(
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
    }

    internal static class PoseSequenceSampler
    {
        internal static bool Apply(
            CombatEntity actor,
            PoseSnapshot sourcePose,
            PoseAuthoringBoneSet authoringBones,
            PoseSequence sequence,
            float time,
            out PoseSequenceSampleResult result)
        {
            result =
                new PoseSequenceSampleResult();

            if (actor == null ||
                sourcePose == null ||
                authoringBones == null ||
                sequence == null ||
                sequence.Keyframes.Count == 0)
            {
                return false;
            }

            PoseSequenceKeyframe from;
            PoseSequenceKeyframe to;

            ResolveKeyframes(
                sequence,
                time,
                result,
                out from,
                out to);

            if (from == null || to == null)
                return false;

            PoseOverrideRuntime.DiscardAll(actor);
            PoseSourceRuntime.Apply(actor);

            HashSet<string> paths =
                new HashSet<string>(
                    StringComparer.Ordinal);

            foreach (PoseSequenceNodeDelta node in from.Nodes)
                paths.Add(node.Path);

            foreach (PoseSequenceNodeDelta node in to.Nodes)
                paths.Add(node.Path);

            List<string> orderedPaths =
                new List<string>(paths);

            orderedPaths.Sort(
                StringComparer.Ordinal);

            for (int i = 0; i < orderedPaths.Count; ++i)
            {
                string path =
                    orderedPaths[i];

                if (!authoringBones.Contains(
                        path))
                {
                    continue;
                }

                PoseNodeSnapshot sourceNode;
                if (!sourcePose.TryGetNode(
                        path,
                        out sourceNode))
                {
                    continue;
                }

                PoseSequenceNodeDelta fromNode;
                PoseSequenceNodeDelta toNode;

                bool hasFrom =
                    from.TryGetNode(
                        path,
                        out fromNode);

                bool hasTo =
                    to.TryGetNode(
                        path,
                        out toNode);

                bool hasPosition =
                    (hasFrom && fromNode.HasPosition) ||
                    (hasTo && toNode.HasPosition);

                bool hasRotation =
                    (hasFrom && fromNode.HasRotation) ||
                    (hasTo && toNode.HasRotation);

                if (hasPosition)
                {
                    Vector3 fromDelta =
                        hasFrom && fromNode.HasPosition
                            ? fromNode.PositionDelta
                            : Vector3.zero;

                    Vector3 toDelta =
                        hasTo && toNode.HasPosition
                            ? toNode.PositionDelta
                            : Vector3.zero;

                    Vector3 position =
                        sourceNode.LocalPosition +
                        Vector3.Lerp(
                            fromDelta,
                            toDelta,
                            result.Factor);

                    PoseOverrideRuntime.SetPosition(
                        actor,
                        path,
                        sourceNode.LocalPosition,
                        position);
                }

                if (hasRotation)
                {
                    Quaternion fromDelta =
                        hasFrom && fromNode.HasRotation
                            ? fromNode.RotationDelta
                            : Quaternion.identity;

                    Quaternion toDelta =
                        hasTo && toNode.HasRotation
                            ? toNode.RotationDelta
                            : Quaternion.identity;

                    Quaternion delta =
                        Quaternion.Slerp(
                            fromDelta,
                            toDelta,
                            result.Factor);

                    Quaternion rotation =
                        sourceNode.LocalRotation *
                        delta;

                    PoseOverrideRuntime.SetRotation(
                        actor,
                        path,
                        sourceNode.LocalRotation,
                        rotation);
                }

                if (hasPosition || hasRotation)
                    ++result.AppliedNodeCount;
            }

            PoseOverrideRuntime.Apply(actor);
            WeaponFollowRuntime.Apply(actor);
            return true;
        }

        private static void ResolveKeyframes(
            PoseSequence sequence,
            float time,
            PoseSequenceSampleResult result,
            out PoseSequenceKeyframe from,
            out PoseSequenceKeyframe to)
        {
            List<PoseSequenceKeyframe> frames =
                sequence.Keyframes;

            float clampedTime =
                Mathf.Clamp(
                    time,
                    0f,
                    Mathf.Max(0.01f, sequence.Length));

            if (frames.Count == 1 ||
                clampedTime <= frames[0].Time)
            {
                from = frames[0];
                to = frames[0];

                result.FromIndex = 0;
                result.ToIndex = 0;
                result.FromTime = from.Time;
                result.ToTime = to.Time;
                result.Factor = 0f;
                return;
            }

            int lastIndex =
                frames.Count - 1;

            if (clampedTime >=
                frames[lastIndex].Time)
            {
                from = frames[lastIndex];
                to = frames[lastIndex];

                result.FromIndex = lastIndex;
                result.ToIndex = lastIndex;
                result.FromTime = from.Time;
                result.ToTime = to.Time;
                result.Factor = 0f;
                return;
            }

            for (int i = 1; i < frames.Count; ++i)
            {
                PoseSequenceKeyframe candidate =
                    frames[i];

                if (candidate.Time <
                    clampedTime)
                {
                    continue;
                }

                from = frames[i - 1];
                to = candidate;

                result.FromIndex = i - 1;
                result.ToIndex = i;
                result.FromTime = from.Time;
                result.ToTime = to.Time;

                float duration =
                    to.Time - from.Time;

                result.Factor =
                    duration > 0.000001f
                        ? Mathf.Clamp01(
                            (clampedTime - from.Time) /
                            duration)
                        : 0f;

                return;
            }

            from = frames[lastIndex];
            to = frames[lastIndex];
            result.FromIndex = lastIndex;
            result.ToIndex = lastIndex;
            result.FromTime = from.Time;
            result.ToTime = to.Time;
            result.Factor = 0f;
        }
    }
}
