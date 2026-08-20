using System;

namespace PB_AnimationLibrary
{
    internal static class PoseCounterpartResolver
    {
        internal static bool TryResolve(
            PoseSnapshot snapshot,
            string sourcePath,
            out PoseNodeSnapshot counterpart)
        {
            counterpart = null;

            if (snapshot == null || string.IsNullOrEmpty(sourcePath))
                return false;

            string counterpartPath;
            if (!TryBuildCounterpartPath(sourcePath, out counterpartPath))
                return false;

            return snapshot.TryGetNode(counterpartPath, out counterpart);
        }

        private static bool TryBuildCounterpartPath(
            string sourcePath,
            out string counterpartPath)
        {
            counterpartPath = null;

            if (TrySwap(sourcePath, "left", "right", out counterpartPath) ||
                TrySwap(sourcePath, "right", "left", out counterpartPath) ||
                TrySwap(sourcePath, "Left", "Right", out counterpartPath) ||
                TrySwap(sourcePath, "Right", "Left", out counterpartPath) ||
                TrySwap(sourcePath, "LEFT", "RIGHT", out counterpartPath) ||
                TrySwap(sourcePath, "RIGHT", "LEFT", out counterpartPath) ||
                TrySwap(sourcePath, "_l_", "_r_", out counterpartPath) ||
                TrySwap(sourcePath, "_r_", "_l_", out counterpartPath) ||
                TrySwap(sourcePath, "_L_", "_R_", out counterpartPath) ||
                TrySwap(sourcePath, "_R_", "_L_", out counterpartPath))
            {
                return true;
            }

            return false;
        }

        private static bool TrySwap(
            string value,
            string sourceToken,
            string targetToken,
            out string result)
        {
            result = null;

            if (value.IndexOf(sourceToken, StringComparison.Ordinal) < 0)
                return false;

            result = value.Replace(sourceToken, targetToken);
            return true;
        }
    }
}
