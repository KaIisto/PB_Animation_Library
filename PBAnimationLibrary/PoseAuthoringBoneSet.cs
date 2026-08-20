using System;
using System.Collections.Generic;
using UnityEngine;

namespace PB_AnimationLibrary
{
    internal sealed class PoseAuthoringBoneSet
    {
        private readonly CombatEntity actor;
        private readonly Transform samplingRoot;
        private readonly HashSet<string> paths;
        private readonly HashSet<string> fingerPaths;

        private PoseAuthoringBoneSet(
            CombatEntity actor,
            Transform samplingRoot,
            HashSet<string> paths,
            HashSet<string> fingerPaths)
        {
            this.actor = actor;
            this.samplingRoot = samplingRoot;
            this.paths = paths;
            this.fingerPaths = fingerPaths;
        }

        internal int Count
        {
            get { return paths.Count; }
        }

        internal int FingerCount
        {
            get { return fingerPaths.Count; }
        }

        internal int BaseCount
        {
            get { return paths.Count - fingerPaths.Count; }
        }

        internal bool Contains(
            string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   paths.Contains(path);
        }

        internal bool IsFinger(
            string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   fingerPaths.Contains(path);
        }

        internal bool CanEdit(
            string path,
            bool includeFingers)
        {
            return Contains(path) &&
                   (includeFingers ||
                    !IsFinger(path));
        }

        internal bool IsBoundTo(
            CombatEntity target)
        {
            if (target == null ||
                target != actor)
            {
                return false;
            }

            Transform currentSamplingRoot;
            Transform jointRoot;

            return VisibleMechRigResolver.TryResolve(
                       target,
                       out currentSamplingRoot,
                       out jointRoot) &&
                   currentSamplingRoot == samplingRoot;
        }

        internal static bool TryCreate(
            CombatEntity actor,
            out PoseAuthoringBoneSet set,
            out string error)
        {
            set = null;
            error = string.Empty;

            if (actor == null ||
                !actor.hasMechAnimationView ||
                actor.mechAnimationView.view == null)
            {
                error = "mech animation view is unavailable";
                return false;
            }

            Transform samplingRoot;
            Transform jointRoot;
            if (!VisibleMechRigResolver.TryResolve(
                    actor,
                    out samplingRoot,
                    out jointRoot))
            {
                error = "visible mech rig is unavailable";
                return false;
            }

            Component viewComponent =
                actor.mechAnimationView.view as Component;

            if (viewComponent == null)
            {
                error = "mech animation view is not a Component";
                return false;
            }

            UnitVisualManager visualManager =
                viewComponent.GetComponentInChildren<UnitVisualManager>(
                    true);

            if (visualManager == null)
            {
                error = "UnitVisualManager is unavailable";
                return false;
            }

            Transform[] primaryBones =
                visualManager.GetIKTransforms();

            if (primaryBones == null ||
                primaryBones.Length == 0)
            {
                primaryBones =
                    BuildPrimaryBonesFromBody(
                        visualManager.body);
            }

            if (primaryBones == null ||
                primaryBones.Length == 0)
            {
                error = "UnitVisualManager primary bone set is empty";
                return false;
            }

            HashSet<string> paths =
                new HashSet<string>(
                    StringComparer.Ordinal);

            HashSet<string> fingerPaths =
                new HashSet<string>(
                    StringComparer.Ordinal);

            for (int i = 0; i < primaryBones.Length; ++i)
            {
                Transform bone =
                    primaryBones[i];

                string path;
                if (bone == null ||
                    !TryBuildPath(
                        samplingRoot,
                        bone,
                        out path))
                {
                    continue;
                }

                if (!paths.Add(path))
                    continue;

                if (IsFingerBone(
                        bone.name))
                {
                    fingerPaths.Add(path);
                }
            }

            if (paths.Count == 0)
            {
                error = "primary bones could not be mapped to the visible rig";
                return false;
            }

            set =
                new PoseAuthoringBoneSet(
                    actor,
                    samplingRoot,
                    paths,
                    fingerPaths);

            return true;
        }

        private static Transform[] BuildPrimaryBonesFromBody(
            SkinnedMeshRenderer body)
        {
            if (body == null ||
                body.bones == null ||
                body.bones.Length == 0)
            {
                return new Transform[0];
            }

            List<Transform> primary =
                new List<Transform>();

            for (int i = 0; i < body.bones.Length; ++i)
            {
                Transform bone =
                    body.bones[i];

                if (bone == null ||
                    bone.name.Contains("auto") ||
                    string.Equals(
                        bone.name,
                        "joint_root",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                primary.Add(bone);
            }

            return primary.ToArray();
        }

        private static bool IsFingerBone(
            string name)
        {
            return !string.IsNullOrEmpty(name) &&
                   name.IndexOf(
                       "finger",
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryBuildPath(
            Transform root,
            Transform target,
            out string path)
        {
            path = string.Empty;

            if (root == null ||
                target == null)
            {
                return false;
            }

            List<string> segments =
                new List<string>();

            Transform current =
                target;

            while (current != null &&
                   current != root)
            {
                segments.Add(
                    current.name);

                current =
                    current.parent;
            }

            if (current != root ||
                segments.Count == 0)
            {
                return false;
            }

            segments.Reverse();

            path =
                string.Join(
                    "/",
                    segments.ToArray());

            return true;
        }
    }
}
