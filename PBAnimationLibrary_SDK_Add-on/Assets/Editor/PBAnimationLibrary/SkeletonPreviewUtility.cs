using UnityEditor;
using UnityEngine;

namespace PB_AnimationLibrary.SDKAuthoring
{
    internal static class SkeletonPreviewUtility
    {
        internal static GameObject CreateTransformOnlyCopy(
            GameObject source)
        {
            if (source == null)
                return null;

            GameObject copy =
                CloneTransformTree(
                    source.transform);

            copy.name =
                "[LOCAL] "
                + source.name
                + "_SkeletonPreview";

            copy.tag = "EditorOnly";

            MarkEditorOnly(
                copy.transform);

            Undo.RegisterCreatedObjectUndo(
                copy,
                "Create PB Animation Preview Skeleton");

            Selection.activeGameObject =
                copy;

            return copy;
        }

        internal static void Draw(
            GameObject root,
            bool showNames)
        {
            if (root == null)
                return;

            Transform[] transforms =
                root.GetComponentsInChildren<Transform>(
                    true);

            for (int i = 0;
                 i < transforms.Length;
                 ++i)
            {
                Transform current =
                    transforms[i];

                if (!IsVisualizedNode(
                        current))
                {
                    continue;
                }

                Transform parent =
                    current.parent;

                if (parent != null &&
                    IsInsideRoot(
                        root.transform,
                        parent) &&
                    IsVisualizedNode(
                        parent))
                {
                    Handles.DrawLine(
                        parent.position,
                        current.position);
                }

                float handleSize =
                    HandleUtility.GetHandleSize(
                        current.position) *
                    0.025f;

                Handles.SphereHandleCap(
                    0,
                    current.position,
                    Quaternion.identity,
                    handleSize,
                    EventType.Repaint);

                if (showNames)
                {
                    Handles.Label(
                        current.position,
                        current.name);
                }
            }
        }

        private static GameObject CloneTransformTree(
            Transform source)
        {
            GameObject copy =
                new GameObject(
                    source.name);

            Transform copyTransform =
                copy.transform;

            copyTransform.localPosition =
                source.localPosition;

            copyTransform.localRotation =
                source.localRotation;

            copyTransform.localScale =
                source.localScale;

            for (int i = 0;
                 i < source.childCount;
                 ++i)
            {
                GameObject childCopy =
                    CloneTransformTree(
                        source.GetChild(i));

                childCopy.transform.SetParent(
                    copyTransform,
                    false);
            }

            return copy;
        }

        private static void MarkEditorOnly(
            Transform root)
        {
            // 로컬 preview rig는 빌드 산출물에 포함되지 않도록 Editor 전용으로 유지한다.
            root.gameObject.hideFlags |=
                HideFlags.DontSaveInBuild;

            for (int i = 0;
                 i < root.childCount;
                 ++i)
            {
                MarkEditorOnly(
                    root.GetChild(i));
            }
        }

        private static bool IsVisualizedNode(
            Transform transform)
        {
            if (transform == null)
                return false;

            string name =
                transform.name;

            // 시각화는 animation skeleton 계열 노드만 그려 대형 prefab의 Scene clutter를 줄인다.
            return name.StartsWith(
                       "joint_",
                       System.StringComparison.Ordinal) ||
                   name.StartsWith(
                       "aim_",
                       System.StringComparison.Ordinal);
        }

        private static bool IsInsideRoot(
            Transform root,
            Transform candidate)
        {
            Transform current =
                candidate;

            while (current != null)
            {
                if (current == root)
                    return true;

                current =
                    current.parent;
            }

            return false;
        }
    }
}
