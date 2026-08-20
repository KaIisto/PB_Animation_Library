using System;
using UnityEngine;

namespace PB_AnimationLibrary
{
    internal static class AnimationLibraryLog
    {
        internal const string Prefix = "[PBAnimLib]";

        internal static void Info(string message)
        {
            Debug.Log(Prefix + " " + message);
        }

        internal static void Warn(string message)
        {
            Debug.LogWarning(Prefix + " " + message);
        }

        internal static void Error(string label, Exception exception)
        {
            Debug.LogError(Prefix + " " + label + " | " + exception);
        }
    }
}
