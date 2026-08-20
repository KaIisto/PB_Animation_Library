using System;
using System.Collections.Generic;
using System.IO;
using PB_AnimationLibrary.Exchange;
using UnityEngine;

namespace PB_AnimationLibrary
{
    internal static class PoseSequenceBakeExchangeExporter
    {
        private const float TimeTolerance = 0.0001f;
        private const float MinimumFrameRate = 1f;
        private const float MaximumFrameRate = 120f;

        private sealed class TrackBuilder
        {
            internal PoseNodeSnapshot SourceNode;
            internal bool HasPosition;
            internal bool HasRotation;
        }

        internal static bool TryExport(
            CombatEntity actor,
            PoseSnapshot sourcePose,
            PoseAuthoringBoneSet authoringBones,
            string sourcePoseName,
            PoseSequence sequence,
            out string outputPath,
            out int trackCount,
            out int authoredTrackCount,
            out int nonAuthoringExcludedTrackCount,
            out bool overwritten,
            out string error)
        {
            outputPath = string.Empty;
            trackCount = 0;
            authoredTrackCount = 0;
            nonAuthoringExcludedTrackCount = 0;
            overwritten = false;
            error = string.Empty;

            if (actor == null)
            {
                error = "actor is null";
                return false;
            }

            if (sourcePose == null)
            {
                error = "source pose is missing";
                return false;
            }

            if (authoringBones == null)
            {
                error = "authoring bone set is missing";
                return false;
            }

            if (sequence == null ||
                sequence.Keyframes.Count == 0)
            {
                error = "sequence has no keyframes";
                return false;
            }

            Dictionary<string, TrackBuilder> builders =
                CollectTrackBuilders(
                    sourcePose,
                    sequence);

            authoredTrackCount =
                builders.Count;

            if (builders.Count == 0)
            {
                error = "sequence has no animated tracks";
                return false;
            }

            nonAuthoringExcludedTrackCount =
                FilterNonAuthoringTrackBuilders(
                    builders,
                    authoringBones);

            if (builders.Count == 0)
            {
                error =
                    "authoring bone filter removed all animated tracks";

                return false;
            }

            FilterTrackBuilders(
                builders,
                sequence.TrackScope);

            if (builders.Count == 0)
            {
                error =
                    "track scope removed all animated tracks: "
                    + PoseSequenceTrackScopeUtility.GetLabel(
                        sequence.TrackScope);

                return false;
            }

            PoseSequenceBakeExchangeFile exchange =
                new PoseSequenceBakeExchangeFile
                {
                    clipName = sequence.Name,
                    sourcePoseName =
                        string.IsNullOrEmpty(sourcePoseName)
                            ? "unknown"
                            : sourcePoseName,
                    frameRate =
                        Mathf.Clamp(
                            sequence.FrameRate,
                            MinimumFrameRate,
                            MaximumFrameRate),
                    duration =
                        Mathf.Max(
                            0.01f,
                            sequence.Length),
                    sourceKeyframeCount =
                        sequence.Keyframes.Count
                };

            List<string> paths =
                new List<string>(
                    builders.Keys);

            paths.Sort(
                StringComparer.Ordinal);

            List<PoseSequenceBakeTrack> tracks =
                new List<PoseSequenceBakeTrack>();

            for (int i = 0; i < paths.Count; ++i)
            {
                string path = paths[i];
                TrackBuilder builder =
                    builders[path];

                PoseSequenceBakeTrack track =
                    BuildTrack(
                        builder,
                        sequence);

                if (track == null)
                    continue;

                tracks.Add(track);
            }

            if (tracks.Count == 0)
            {
                error = "bake track generation returned zero tracks";
                return false;
            }

            exchange.tracks =
                tracks.ToArray();

            string exportDirectory =
                Path.Combine(
                    Application.persistentDataPath,
                    "PBAnimationLibrary",
                    "PoseSequenceExports");

            Directory.CreateDirectory(
                exportDirectory);

            string fileName =
                SanitizeFileName(
                    exchange.clipName)
                + ".pbalibpose.json";

            outputPath =
                Path.Combine(
                    exportDirectory,
                    fileName);

            string json;
            string serializationError;

            if (!PoseSequenceBakeJsonWriter.TrySerialize(
                    exchange,
                    out json,
                    out serializationError))
            {
                error =
                    "JSON serialization failed: "
                    + serializationError;

                outputPath = string.Empty;
                return false;
            }

            overwritten =
                File.Exists(
                    outputPath);

            try
            {
                File.WriteAllText(
                    outputPath,
                    json);
            }
            catch (Exception exception)
            {
                error =
                    "file write failed: "
                    + exception.GetType().Name
                    + ": "
                    + exception.Message;

                outputPath = string.Empty;
                return false;
            }

            trackCount =
                exchange.tracks.Length;

            return true;
        }

        private static Dictionary<string, TrackBuilder> CollectTrackBuilders(
            PoseSnapshot sourcePose,
            PoseSequence sequence)
        {
            Dictionary<string, TrackBuilder> builders =
                new Dictionary<string, TrackBuilder>(
                    StringComparer.Ordinal);

            List<PoseSequenceKeyframe> keyframes =
                sequence.Keyframes;

            for (int frameIndex = 0;
                 frameIndex < keyframes.Count;
                 ++frameIndex)
            {
                PoseSequenceKeyframe keyframe =
                    keyframes[frameIndex];

                foreach (PoseSequenceNodeDelta delta in keyframe.Nodes)
                {
                    if (delta == null ||
                        string.IsNullOrEmpty(delta.Path))
                    {
                        continue;
                    }

                    PoseNodeSnapshot sourceNode;
                    if (!sourcePose.TryGetNode(
                            delta.Path,
                            out sourceNode))
                    {
                        continue;
                    }

                    TrackBuilder builder;
                    if (!builders.TryGetValue(
                            delta.Path,
                            out builder))
                    {
                        builder =
                            new TrackBuilder
                            {
                                SourceNode = sourceNode
                            };

                        builders.Add(
                            delta.Path,
                            builder);
                    }

                    builder.HasPosition |=
                        delta.HasPosition;

                    builder.HasRotation |=
                        delta.HasRotation;
                }
            }

            List<string> emptyPaths =
                new List<string>();

            foreach (KeyValuePair<string, TrackBuilder> pair in builders)
            {
                TrackBuilder builder = pair.Value;

                if (!builder.HasPosition &&
                    !builder.HasRotation)
                {
                    emptyPaths.Add(pair.Key);
                }
            }

            for (int i = 0; i < emptyPaths.Count; ++i)
                builders.Remove(emptyPaths[i]);

            return builders;
        }

        private static int FilterNonAuthoringTrackBuilders(
            Dictionary<string, TrackBuilder> builders,
            PoseAuthoringBoneSet authoringBones)
        {
            List<string> excludedPaths =
                new List<string>();

            foreach (KeyValuePair<string, TrackBuilder> pair in builders)
            {
                if (!authoringBones.Contains(
                        pair.Key))
                {
                    excludedPaths.Add(
                        pair.Key);
                }
            }

            for (int i = 0; i < excludedPaths.Count; ++i)
            {
                builders.Remove(
                    excludedPaths[i]);
            }

            return excludedPaths.Count;
        }

        private static void FilterTrackBuilders(
            Dictionary<string, TrackBuilder> builders,
            PoseSequenceTrackScope scope)
        {
            if (scope == PoseSequenceTrackScope.EditedNodes)
                return;

            List<string> excludedPaths =
                new List<string>();

            foreach (KeyValuePair<string, TrackBuilder> pair in builders)
            {
                if (!PoseSequenceTrackScopeUtility.IncludesPath(
                        scope,
                        pair.Key))
                {
                    excludedPaths.Add(
                        pair.Key);
                }
            }

            for (int i = 0; i < excludedPaths.Count; ++i)
            {
                builders.Remove(
                    excludedPaths[i]);
            }
        }

        private static PoseSequenceBakeTrack BuildTrack(
            TrackBuilder builder,
            PoseSequence sequence)
        {
            if (builder == null ||
                builder.SourceNode == null)
            {
                return null;
            }

            PoseNodeSnapshot sourceNode =
                builder.SourceNode;

            PoseSequenceBakeTrack track =
                new PoseSequenceBakeTrack
                {
                    path = sourceNode.Path,
                    pathHash =
                        sourceNode.PathHash.ToString("X8"),
                    hasPosition =
                        builder.HasPosition,
                    hasRotation =
                        builder.HasRotation
                };

            List<PoseSequenceBakePositionKey> positionKeys =
                new List<PoseSequenceBakePositionKey>();

            List<PoseSequenceBakeRotationKey> rotationKeys =
                new List<PoseSequenceBakeRotationKey>();

            List<PoseSequenceKeyframe> keyframes =
                sequence.Keyframes;

            if (keyframes.Count == 0)
                return null;

            PoseSequenceKeyframe first =
                keyframes[0];

            PoseSequenceKeyframe last =
                keyframes[keyframes.Count - 1];

            if (first.Time > TimeTolerance)
            {
                AppendKey(
                    track,
                    positionKeys,
                    rotationKeys,
                    sourceNode,
                    first,
                    0f);
            }

            for (int i = 0;
                 i < keyframes.Count;
                 ++i)
            {
                PoseSequenceKeyframe keyframe =
                    keyframes[i];

                AppendKey(
                    track,
                    positionKeys,
                    rotationKeys,
                    sourceNode,
                    keyframe,
                    keyframe.Time);
            }

            float duration =
                Mathf.Max(
                    0.01f,
                    sequence.Length);

            if (duration -
                last.Time >
                TimeTolerance)
            {
                AppendKey(
                    track,
                    positionKeys,
                    rotationKeys,
                    sourceNode,
                    last,
                    duration);
            }

            if (track.hasRotation)
                EnsureRotationContinuity(
                    rotationKeys);

            track.positionKeys =
                positionKeys.ToArray();

            track.rotationKeys =
                rotationKeys.ToArray();

            return track;
        }

        private static void AppendKey(
            PoseSequenceBakeTrack track,
            List<PoseSequenceBakePositionKey> positionKeys,
            List<PoseSequenceBakeRotationKey> rotationKeys,
            PoseNodeSnapshot sourceNode,
            PoseSequenceKeyframe keyframe,
            float time)
        {
            PoseSequenceNodeDelta delta;
            bool hasDelta =
                keyframe.TryGetNode(
                    sourceNode.Path,
                    out delta);

            if (track.hasPosition)
            {
                Vector3 position =
                    sourceNode.LocalPosition;

                if (hasDelta &&
                    delta.HasPosition)
                {
                    position +=
                        delta.PositionDelta;
                }

                positionKeys.Add(
                    new PoseSequenceBakePositionKey
                    {
                        time = time,
                        x = position.x,
                        y = position.y,
                        z = position.z
                    });
            }

            if (track.hasRotation)
            {
                Quaternion rotation =
                    sourceNode.LocalRotation;

                if (hasDelta &&
                    delta.HasRotation)
                {
                    rotation =
                        Normalize(
                            sourceNode.LocalRotation *
                            delta.RotationDelta);
                }

                rotationKeys.Add(
                    new PoseSequenceBakeRotationKey
                    {
                        time = time,
                        x = rotation.x,
                        y = rotation.y,
                        z = rotation.z,
                        w = rotation.w
                    });
            }
        }

        private static void EnsureRotationContinuity(
            List<PoseSequenceBakeRotationKey> keys)
        {
            if (keys == null ||
                keys.Count < 2)
            {
                return;
            }

            Quaternion previous =
                ToQuaternion(
                    keys[0]);

            for (int i = 1; i < keys.Count; ++i)
            {
                PoseSequenceBakeRotationKey key =
                    keys[i];

                Quaternion current =
                    ToQuaternion(key);

                if (Quaternion.Dot(
                        previous,
                        current) < 0f)
                {
                    current =
                        new Quaternion(
                            -current.x,
                            -current.y,
                            -current.z,
                            -current.w);

                    key.x = current.x;
                    key.y = current.y;
                    key.z = current.z;
                    key.w = current.w;
                }

                previous = current;
            }
        }

        private static Quaternion ToQuaternion(
            PoseSequenceBakeRotationKey key)
        {
            return Normalize(
                new Quaternion(
                    key.x,
                    key.y,
                    key.z,
                    key.w));
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

            float inverse =
                1f / magnitude;

            return new Quaternion(
                rotation.x * inverse,
                rotation.y * inverse,
                rotation.z * inverse,
                rotation.w * inverse);
        }

        private static string SanitizeFileName(
            string value)
        {
            string source =
                string.IsNullOrEmpty(value)
                    ? "pbalib_pose_sequence"
                    : value.Trim();

            char[] invalid =
                Path.GetInvalidFileNameChars();

            for (int i = 0; i < invalid.Length; ++i)
            {
                source =
                    source.Replace(
                        invalid[i],
                        '_');
            }

            return source.Length > 0
                ? source
                : "pbalib_pose_sequence";
        }
    }
}
