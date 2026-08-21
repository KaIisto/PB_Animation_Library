using System;

namespace PB_AnimationLibrary
{
    internal enum PoseSequenceTrackScope
    {
        EditedNodes = 0,
        UpperBodyPreserveHead = 1,
        UpperBody = 2,
        LowerBody = 3,
        HeadOnly = 4,
        LeftArm = 5,
        RightArm = 6,
        BothArms = 7
    }

    internal static class PoseSequenceTrackScopeUtility
    {
        internal static readonly string[] Labels =
        {
            "Edited Nodes",
            "Upper Body - Preserve Head",
            "Upper Body",
            "Lower Body",
            "Head Only",
            "Left Arm",
            "Right Arm",
            "Both Arms"
        };

        internal static string GetLabel(
            PoseSequenceTrackScope scope)
        {
            int index = (int)scope;

            if (index < 0 ||
                index >= Labels.Length)
            {
                return Labels[0];
            }

            return Labels[index];
        }

        internal static bool IncludesPath(
            PoseSequenceTrackScope scope,
            string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (scope == PoseSequenceTrackScope.EditedNodes)
                return true;

            switch (scope)
            {
                case PoseSequenceTrackScope.UpperBodyPreserveHead:
                    return IsTorsoPath(path) ||
                           IsLeftArmPath(path) ||
                           IsRightArmPath(path);

                case PoseSequenceTrackScope.UpperBody:
                    return IsTorsoPath(path) ||
                           IsHeadPath(path) ||
                           IsLeftArmPath(path) ||
                           IsRightArmPath(path);

                case PoseSequenceTrackScope.LowerBody:
                    return IsPelvisPath(path) ||
                           IsLeftLegPath(path) ||
                           IsRightLegPath(path);

                case PoseSequenceTrackScope.HeadOnly:
                    return IsHeadPath(path);

                case PoseSequenceTrackScope.LeftArm:
                    return IsLeftArmPath(path);

                case PoseSequenceTrackScope.RightArm:
                    return IsRightArmPath(path);

                case PoseSequenceTrackScope.BothArms:
                    return IsLeftArmPath(path) ||
                           IsRightArmPath(path);

                default:
                    return true;
            }
        }

        private static bool IsTorsoPath(
            string path)
        {
            string nodeName =
                GetNodeName(path);

            return string.Equals(
                nodeName,
                "joint_torso_xy",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHeadPath(
            string path)
        {
            return ContainsSegment(
                path,
                "joint_head_xy");
        }

        private static bool IsLeftArmPath(
            string path)
        {
            return ContainsSegment(
                path,
                "joint_left_arm_xyz");
        }

        private static bool IsRightArmPath(
            string path)
        {
            return ContainsSegment(
                path,
                "joint_right_arm_xyz");
        }

        private static bool IsPelvisPath(
            string path)
        {
            string nodeName =
                GetNodeName(path);

            return string.Equals(
                nodeName,
                "joint_pelvis_xyz",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLeftLegPath(
            string path)
        {
            return ContainsSegment(
                path,
                "joint_left_thigh_xyz");
        }

        private static bool IsRightLegPath(
            string path)
        {
            return ContainsSegment(
                path,
                "joint_right_thigh_xyz");
        }

        private static bool ContainsSegment(
            string path,
            string segment)
        {
            string[] segments =
                path.Split('/');

            for (int i = 0; i < segments.Length; ++i)
            {
                if (string.Equals(
                        segments[i],
                        segment,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetNodeName(
            string path)
        {
            int separator =
                path.LastIndexOf('/');

            return separator >= 0
                ? path.Substring(separator + 1)
                : path;
        }
    }
}
