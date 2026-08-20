using System;
using System.Collections.Generic;
using UnityEngine;

namespace PB_AnimationLibrary
{
    internal sealed class AnimationClipValidationEntry
    {
        internal string BundleName;
        internal string AssetPath;
        internal AnimationClip Clip;

        internal string DisplayId
        {
            get
            {
                return BundleName
                    + ":"
                    + (Clip != null
                        ? Clip.name
                        : "(missing)");
            }
        }
    }

    internal static class AnimationClipValidationCatalog
    {
        private static readonly List<AnimationClipValidationEntry> entries =
            new List<AnimationClipValidationEntry>();

        internal static int Count
        {
            get { return entries.Count; }
        }

        internal static int Refresh()
        {
            entries.Clear();

            int bundleCount = 0;
            int animationAssetCount = 0;

            foreach (AssetBundle bundle in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (bundle == null)
                    continue;

                ++bundleCount;

                string bundleName =
                    string.IsNullOrEmpty(bundle.name)
                        ? "(unnamed)"
                        : bundle.name;

                string[] assetNames;
                try
                {
                    assetNames =
                        bundle.GetAllAssetNames();
                }
                catch (Exception exception)
                {
                    AnimationLibraryLog.Warn(
                        "AnimationValidation catalog scan failed"
                        + "|bundle="
                        + bundleName
                        + "|error="
                        + exception.GetType().Name);

                    continue;
                }

                for (int i = 0; i < assetNames.Length; ++i)
                {
                    string assetPath =
                        assetNames[i];

                    if (string.IsNullOrEmpty(assetPath) ||
                        !assetPath.EndsWith(
                            ".anim",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    ++animationAssetCount;

                    AnimationClip clip;
                    try
                    {
                        clip =
                            bundle.LoadAsset<AnimationClip>(
                                assetPath);
                    }
                    catch (Exception exception)
                    {
                        AnimationLibraryLog.Warn(
                            "AnimationValidation clip load failed"
                            + "|bundle="
                            + bundleName
                            + "|asset="
                            + assetPath
                            + "|error="
                            + exception.GetType().Name);

                        continue;
                    }

                    if (clip == null)
                        continue;

                    entries.Add(
                        new AnimationClipValidationEntry
                        {
                            BundleName = bundleName,
                            AssetPath = assetPath,
                            Clip = clip
                        });
                }
            }

            entries.Sort(
                CompareEntries);

            AnimationLibraryLog.Info(
                "AnimationValidation|CATALOG_REFRESH"
                + "|bundles="
                + bundleCount
                + "|animationAssets="
                + animationAssetCount
                + "|loaded="
                + entries.Count);

            return entries.Count;
        }

        internal static void FindMatches(
            string filter,
            List<AnimationClipValidationEntry> results)
        {
            if (results == null)
                return;

            results.Clear();

            string normalizedFilter =
                string.IsNullOrEmpty(filter)
                    ? string.Empty
                    : filter.Trim();

            for (int i = 0; i < entries.Count; ++i)
            {
                AnimationClipValidationEntry entry =
                    entries[i];

                if (entry == null ||
                    entry.Clip == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(normalizedFilter) ||
                    entry.BundleName.IndexOf(
                        normalizedFilter,
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    entry.AssetPath.IndexOf(
                        normalizedFilter,
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    entry.Clip.name.IndexOf(
                        normalizedFilter,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    results.Add(
                        entry);
                }
            }
        }

        private static int CompareEntries(
            AnimationClipValidationEntry left,
            AnimationClipValidationEntry right)
        {
            int bundleComparison =
                string.Compare(
                    left != null ? left.BundleName : string.Empty,
                    right != null ? right.BundleName : string.Empty,
                    StringComparison.OrdinalIgnoreCase);

            if (bundleComparison != 0)
                return bundleComparison;

            string leftName =
                left != null && left.Clip != null
                    ? left.Clip.name
                    : string.Empty;

            string rightName =
                right != null && right.Clip != null
                    ? right.Clip.name
                    : string.Empty;

            int nameComparison =
                string.Compare(
                    leftName,
                    rightName,
                    StringComparison.OrdinalIgnoreCase);

            if (nameComparison != 0)
                return nameComparison;

            return string.Compare(
                left != null ? left.AssetPath : string.Empty,
                right != null ? right.AssetPath : string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
