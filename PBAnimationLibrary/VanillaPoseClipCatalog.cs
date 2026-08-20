using System;
using System.Collections.Generic;
using UnityEngine;

namespace PB_AnimationLibrary
{
    internal static class VanillaPoseClipCatalog
    {
        private const string PosePrefix = "customization_pose_";
        private const string PosesPrefix = "customization_poses_";

        private static readonly List<AnimationClip> customizationClips =
            new List<AnimationClip>();

        private static readonly List<AnimationClip> symmetricBaseClips =
            new List<AnimationClip>();

        private static readonly List<AnimationClip> otherCustomizationClips =
            new List<AnimationClip>();

        private static bool scanned;
        private static int loadedClipCount;

        internal static List<AnimationClip> CustomizationClips
        {
            get { return customizationClips; }
        }

        internal static List<AnimationClip> SymmetricBaseClips
        {
            get { return symmetricBaseClips; }
        }

        internal static List<AnimationClip> OtherCustomizationClips
        {
            get { return otherCustomizationClips; }
        }

        internal static bool HasScanned
        {
            get { return scanned; }
        }

        internal static int LoadedClipCount
        {
            get { return loadedClipCount; }
        }

        internal static void Refresh()
        {
            customizationClips.Clear();
            symmetricBaseClips.Clear();
            otherCustomizationClips.Clear();

            AnimationClip[] loaded =
                Resources.FindObjectsOfTypeAll<AnimationClip>();

            loadedClipCount = loaded != null ? loaded.Length : 0;

            Dictionary<string, AnimationClip> candidates =
                new Dictionary<string, AnimationClip>(
                    StringComparer.OrdinalIgnoreCase);

            if (loaded != null)
            {
                for (int i = 0; i < loaded.Length; ++i)
                {
                    AnimationClip clip = loaded[i];
                    if (clip == null ||
                        string.IsNullOrEmpty(clip.name) ||
                        !IsCustomizationPoseCandidate(clip.name))
                    {
                        continue;
                    }

                    if (!candidates.ContainsKey(clip.name))
                        candidates.Add(clip.name, clip);
                }
            }

            foreach (KeyValuePair<string, AnimationClip> pair in candidates)
            {
                AnimationClip clip = pair.Value;
                customizationClips.Add(clip);

                // PB 2.2.2에서 *_A는 메크 골격의 좌우 대칭 static base pose로 확인됨
                if (IsSymmetricBasePose(clip.name))
                    symmetricBaseClips.Add(clip);
                else
                    otherCustomizationClips.Add(clip);
            }

            customizationClips.Sort(CompareClipsByName);
            symmetricBaseClips.Sort(CompareClipsByName);
            otherCustomizationClips.Sort(CompareClipsByName);

            scanned = true;

            AnimationLibraryLog.Info(
                "PoseLab|CUSTOMIZATION_CLIPS_DISCOVERED"
                + "|loadedAnimationClips=" + loadedClipCount
                + "|candidates=" + customizationClips.Count
                + "|symmetricBase=" + symmetricBaseClips.Count
                + "|other=" + otherCustomizationClips.Count);
        }

        internal static bool IsSymmetricBasePose(string name)
        {
            return !string.IsNullOrEmpty(name) &&
                   name.EndsWith(
                       "_A",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCustomizationPoseCandidate(string name)
        {
            bool prefixMatch =
                name.StartsWith(
                    PosePrefix,
                    StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(
                    PosesPrefix,
                    StringComparison.OrdinalIgnoreCase);

            if (!prefixMatch)
                return false;

            if (name.EndsWith(
                    "_intro",
                    StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(
                    "_settle",
                    StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(
                    "_vr",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static int CompareClipsByName(
            AnimationClip left,
            AnimationClip right)
        {
            string leftName =
                left != null ? left.name : string.Empty;

            string rightName =
                right != null ? right.name : string.Empty;

            return string.Compare(
                leftName,
                rightName,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
