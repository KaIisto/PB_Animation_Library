using UnityEngine;

namespace PB_AnimationLibrary
{
    internal static class PoseSymmetry
    {
        internal static bool MirrorAbsoluteRotation(
            CombatEntity actor,
            PoseSnapshot sourcePose,
            PoseNodeSnapshot sourceNode,
            PoseNodeSnapshot targetNode,
            out float targetChangeDegrees,
            out float mirrorErrorDegrees)
        {
            targetChangeDegrees = 0f;
            mirrorErrorDegrees = 0f;

            if (actor == null ||
                sourcePose == null ||
                sourceNode == null ||
                targetNode == null)
            {
                return false;
            }

            Quaternion sourceCurrentRoot;
            Quaternion targetCurrentRoot;

            if (!TryGetRootRotation(
                    actor,
                    sourcePose,
                    sourceNode.Path,
                    true,
                    out sourceCurrentRoot) ||
                !TryGetRootRotation(
                    actor,
                    sourcePose,
                    targetNode.Path,
                    true,
                    out targetCurrentRoot))
            {
                return false;
            }

            Quaternion desiredTargetRoot =
                MirrorRootSpaceRotation(sourceCurrentRoot);

            Quaternion targetParentRoot = Quaternion.identity;
            if (!string.IsNullOrEmpty(targetNode.ParentPath) &&
                !TryGetRootRotation(
                    actor,
                    sourcePose,
                    targetNode.ParentPath,
                    true,
                    out targetParentRoot))
            {
                return false;
            }

            Quaternion targetLocal =
                Quaternion.Inverse(targetParentRoot) * desiredTargetRoot;

            targetLocal = Normalize(targetLocal);

            targetChangeDegrees =
                Quaternion.Angle(targetCurrentRoot, desiredTargetRoot);

            PoseOverrideRuntime.SetRotation(
                actor,
                targetNode.Path,
                targetNode.LocalRotation,
                targetLocal);

            Quaternion targetAppliedRoot;
            if (TryGetRootRotation(
                    actor,
                    sourcePose,
                    targetNode.Path,
                    true,
                    out targetAppliedRoot))
            {
                mirrorErrorDegrees =
                    Quaternion.Angle(desiredTargetRoot, targetAppliedRoot);
            }

            return true;
        }

        internal static bool MirrorRotationEdit(
            CombatEntity actor,
            PoseSnapshot sourcePose,
            PoseNodeSnapshot sourceNode,
            PoseNodeSnapshot targetNode,
            out float sourceDeltaDegrees,
            out float targetDeltaDegrees)
        {
            sourceDeltaDegrees = 0f;
            targetDeltaDegrees = 0f;

            if (actor == null ||
                sourcePose == null ||
                sourceNode == null ||
                targetNode == null)
            {
                return false;
            }

            Quaternion sourceBaseRoot;
            Quaternion sourceCurrentRoot;
            Quaternion targetBaseRoot;

            if (!TryGetRootRotation(
                    actor,
                    sourcePose,
                    sourceNode.Path,
                    false,
                    out sourceBaseRoot) ||
                !TryGetRootRotation(
                    actor,
                    sourcePose,
                    sourceNode.Path,
                    true,
                    out sourceCurrentRoot) ||
                !TryGetRootRotation(
                    actor,
                    sourcePose,
                    targetNode.Path,
                    false,
                    out targetBaseRoot))
            {
                return false;
            }

            Quaternion sourceDelta =
                sourceCurrentRoot * Quaternion.Inverse(sourceBaseRoot);

            Quaternion mirroredDelta =
                MirrorRootSpaceRotation(sourceDelta);

            Quaternion desiredTargetRoot =
                mirroredDelta * targetBaseRoot;

            Quaternion targetParentRoot = Quaternion.identity;
            if (!string.IsNullOrEmpty(targetNode.ParentPath) &&
                !TryGetRootRotation(
                    actor,
                    sourcePose,
                    targetNode.ParentPath,
                    true,
                    out targetParentRoot))
            {
                return false;
            }

            Quaternion targetLocal =
                Quaternion.Inverse(targetParentRoot) * desiredTargetRoot;

            targetLocal = Normalize(targetLocal);

            PoseOverrideRuntime.SetRotation(
                actor,
                targetNode.Path,
                targetNode.LocalRotation,
                targetLocal);

            sourceDeltaDegrees =
                Quaternion.Angle(sourceBaseRoot, sourceCurrentRoot);

            Quaternion targetCurrentRoot;
            if (TryGetRootRotation(
                    actor,
                    sourcePose,
                    targetNode.Path,
                    true,
                    out targetCurrentRoot))
            {
                targetDeltaDegrees =
                    Quaternion.Angle(targetBaseRoot, targetCurrentRoot);
            }

            return true;
        }

        private static bool TryGetRootRotation(
            CombatEntity actor,
            PoseSnapshot sourcePose,
            string path,
            bool includeOverrides,
            out Quaternion rootRotation)
        {
            rootRotation = Quaternion.identity;

            PoseNodeSnapshot node;
            if (!sourcePose.TryGetNode(path, out node))
                return false;

            Quaternion localRotation = node.LocalRotation;

            if (includeOverrides)
            {
                Quaternion overrideRotation;
                if (PoseOverrideRuntime.TryGetRotation(
                        actor,
                        path,
                        out overrideRotation))
                {
                    localRotation = overrideRotation;
                }
            }

            if (string.IsNullOrEmpty(node.ParentPath))
            {
                // mirror 계산은 joint_root 자체가 아니라 joint_root local 좌표계를 기준으로 함
                rootRotation = Quaternion.identity;
                return true;
            }

            Quaternion parentRoot;
            if (!TryGetRootRotation(
                    actor,
                    sourcePose,
                    node.ParentPath,
                    includeOverrides,
                    out parentRoot))
            {
                return false;
            }

            rootRotation = parentRoot * localRotation;
            return true;
        }

        private static Quaternion MirrorRootSpaceRotation(Quaternion rotation)
        {
            // joint_root local X=0 평면 반사는 회전축의 Y/Z 성분 부호를 뒤집는 conjugation으로 표현
            return Normalize(
                new Quaternion(
                    rotation.x,
                    -rotation.y,
                    -rotation.z,
                    rotation.w));
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
    }
}
