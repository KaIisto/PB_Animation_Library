using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PB_AnimationLibrary.SDKAuthoring
{
    internal static class SkeletonVisualProxyUtility
    {
        private const string ProxyNamePrefix =
            "[PBAL Proxy] ";

        private static readonly VisualBinding[] Bindings =
        {
            VisualBinding.Center(
                "part_armor-name_head",
                "joint_head_xy"),

            VisualBinding.Center(
                "part_armor-name_pelvis",
                "joint_pelvis_xyz"),

            VisualBinding.Center(
                "part_armor-name_torso",
                "joint_torso_xy"),

            VisualBinding.MirroredPair(
                "part_armor-name_shoulder",
                "joint_right_arm_xyz",
                "joint_left_arm_xyz"),

            VisualBinding.MirroredPair(
                "part_armor-name_arm",
                "joint_right_forearm_x",
                "joint_left_forearm_x"),

            VisualBinding.MirroredPair(
                "part_armor-name_thigh",
                "joint_right_thigh_xyz",
                "joint_left_thigh_xyz"),

            VisualBinding.MirroredPair(
                "part_armor-name_leg",
                "joint_right_leg_x",
                "joint_left_leg_x"),

            VisualBinding.MirroredPair(
                "part_armor-name_foot",
                "joint_right_foot_xyz",
                "joint_left_foot_xyz"),

            VisualBinding.MirroredPair(
                "part_armor-name_foot_front",
                "joint_right_foot_front_x",
                "joint_left_foot_front_x"),

            VisualBinding.MirroredPair(
                "part_armor-name_foot_tongue",
                "joint_right_foot_tongue_x",
                "joint_left_foot_tongue_x")
        };

        internal static bool TryBuildReplaceProxy(
            GameObject previewRoot,
            GameObject sourceAsset,
            out int visualCount,
            out string result)
        {
            visualCount = 0;
            result = string.Empty;

            if (previewRoot == null ||
                !previewRoot.scene.IsValid() ||
                !previewRoot.scene.isLoaded)
            {
                result =
                    "preview root must be a loaded Scene GameObject";

                return false;
            }

            if (sourceAsset == null ||
                !AssetDatabase.Contains(
                    sourceAsset))
            {
                result =
                    "reference model must be a Project asset";

                return false;
            }

            Remove(
                previewRoot);

            GameObject sourceInstance = null;

            try
            {
                sourceInstance =
                    UnityEngine.Object.Instantiate(
                        sourceAsset);

                sourceInstance.name =
                    "[PBAL Temp] "
                    + sourceAsset.name;

                sourceInstance.hideFlags =
                    HideFlags.HideAndDontSave;

                Transform sourceTransform =
                    sourceInstance.transform;

                // Model importer의 축 보정은 유지하고 preview root 원점에 reference를 정렬
                sourceTransform.SetParent(
                    previewRoot.transform,
                    false);

                sourceTransform.localPosition =
                    Vector3.zero;

                List<string> missingSources =
                    new List<string>();

                List<string> missingTargets =
                    new List<string>();

                for (int i = 0;
                     i < Bindings.Length;
                     ++i)
                {
                    VisualBinding binding =
                        Bindings[i];

                    Transform template =
                        FindDescendantByName(
                            sourceTransform,
                            binding.SourceName);

                    if (template == null)
                    {
                        missingSources.Add(
                            binding.SourceName);

                        continue;
                    }

                    if (binding.IsMirroredPair)
                    {
                        Transform rightTarget =
                            FindDescendantByName(
                                previewRoot.transform,
                                binding.RightJoint);

                        Transform leftTarget =
                            FindDescendantByName(
                                previewRoot.transform,
                                binding.LeftJoint);

                        if (rightTarget == null)
                        {
                            missingTargets.Add(
                                binding.RightJoint);
                        }

                        if (leftTarget == null)
                        {
                            missingTargets.Add(
                                binding.LeftJoint);
                        }

                        if (rightTarget == null ||
                            leftTarget == null)
                        {
                            continue;
                        }

                        GameObject leftVisual =
                            CreateVisualClone(
                                template,
                                binding.SourceName
                                + "_left");

                        if (leftVisual == null)
                            continue;

                        BindVisualToJoint(
                            leftVisual.transform,
                            leftTarget,
                            false);

                        ++visualCount;

                        GameObject rightVisual =
                            CreateVisualClone(
                                template,
                                binding.SourceName
                                + "_right");

                        if (rightVisual == null)
                            continue;

                        BindVisualToJoint(
                            rightVisual.transform,
                            rightTarget,
                            true);

                        ++visualCount;
                    }
                    else
                    {
                        Transform target =
                            FindDescendantByName(
                                previewRoot.transform,
                                binding.CenterJoint);

                        if (target == null)
                        {
                            missingTargets.Add(
                                binding.CenterJoint);

                            continue;
                        }

                        GameObject visual =
                            CreateVisualClone(
                                template,
                                binding.SourceName);

                        if (visual == null)
                            continue;

                        BindVisualToJoint(
                            visual.transform,
                            target,
                            false);

                        ++visualCount;
                    }
                }

                result =
                    BuildResultText(
                        visualCount,
                        missingSources,
                        missingTargets);

                SetVisible(
                    previewRoot,
                    true);

                return visualCount > 0;
            }
            catch (Exception exception)
            {
                Remove(
                    previewRoot);

                result =
                    exception.GetType().Name
                    + ": "
                    + exception.Message;

                return false;
            }
            finally
            {
                if (sourceInstance != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        sourceInstance);
                }
            }
        }

        internal static void SetVisible(
            GameObject previewRoot,
            bool visible)
        {
            if (previewRoot == null)
                return;

            Transform[] transforms =
                previewRoot.GetComponentsInChildren<Transform>(
                    true);

            for (int i = 0;
                 i < transforms.Length;
                 ++i)
            {
                Transform transform =
                    transforms[i];

                if (!IsProxyRoot(
                        transform))
                {
                    continue;
                }

                transform.gameObject.SetActive(
                    visible);
            }
        }

        internal static int Remove(
            GameObject previewRoot)
        {
            if (previewRoot == null)
                return 0;

            Transform[] transforms =
                previewRoot.GetComponentsInChildren<Transform>(
                    true);

            List<GameObject> roots =
                new List<GameObject>();

            for (int i = 0;
                 i < transforms.Length;
                 ++i)
            {
                Transform transform =
                    transforms[i];

                if (IsProxyRoot(
                        transform))
                {
                    roots.Add(
                        transform.gameObject);
                }
            }

            for (int i = 0;
                 i < roots.Count;
                 ++i)
            {
                Undo.DestroyObjectImmediate(
                    roots[i]);
            }

            return roots.Count;
        }

        internal static bool HasProxy(
            GameObject previewRoot)
        {
            if (previewRoot == null)
                return false;

            Transform[] transforms =
                previewRoot.GetComponentsInChildren<Transform>(
                    true);

            for (int i = 0;
                 i < transforms.Length;
                 ++i)
            {
                if (IsProxyRoot(
                        transforms[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static void BindVisualToJoint(
            Transform visual,
            Transform target,
            bool mirrorLocalX)
        {
            if (visual == null ||
                target == null)
            {
                return;
            }

            Vector3 sourceScale =
                visual.localScale;

            // SDK armor reference의 node TRS는 정적 제작 pose이므로
            // mesh-local pivot만 animation joint에 결합한다.
            visual.SetParent(
                target,
                false);

            visual.localPosition =
                Vector3.zero;

            visual.localRotation =
                Quaternion.identity;

            if (mirrorLocalX)
            {
                sourceScale.x =
                    -sourceScale.x;
            }

            visual.localScale =
                sourceScale;
        }

        private static GameObject CreateVisualClone(
            Transform template,
            string label)
        {
            GameObject clone =
                UnityEngine.Object.Instantiate(
                    template.gameObject,
                    template.parent,
                    false);

            clone.name =
                ProxyNamePrefix
                + label;

            StripNonVisualComponents(
                clone.transform);

            if (clone.GetComponentsInChildren<MeshRenderer>(
                    true).Length == 0)
            {
                UnityEngine.Object.DestroyImmediate(
                    clone);

                return null;
            }

            clone.tag =
                "EditorOnly";

            MarkEditorOnly(
                clone.transform);

            Undo.RegisterCreatedObjectUndo(
                clone,
                "Create PB Animation Visual Proxy");

            return clone;
        }

        private static void StripNonVisualComponents(
            Transform root)
        {
            Component[] components =
                root.GetComponents<Component>();

            for (int i = 0;
                 i < components.Length;
                 ++i)
            {
                Component component =
                    components[i];

                if (component == null ||
                    component is Transform ||
                    component is MeshFilter ||
                    component is MeshRenderer)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(
                    component);
            }

            for (int i = 0;
                 i < root.childCount;
                 ++i)
            {
                StripNonVisualComponents(
                    root.GetChild(i));
            }
        }

        private static void MarkEditorOnly(
            Transform root)
        {
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

        private static Transform FindDescendantByName(
            Transform root,
            string name)
        {
            if (root == null ||
                string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (string.Equals(
                    root.name,
                    name,
                    StringComparison.Ordinal))
            {
                return root;
            }

            for (int i = 0;
                 i < root.childCount;
                 ++i)
            {
                Transform result =
                    FindDescendantByName(
                        root.GetChild(i),
                        name);

                if (result != null)
                    return result;
            }

            return null;
        }

        private static bool IsProxyRoot(
            Transform transform)
        {
            return transform != null &&
                   transform.name.StartsWith(
                       ProxyNamePrefix,
                       StringComparison.Ordinal);
        }

        private static string BuildResultText(
            int visualCount,
            List<string> missingSources,
            List<string> missingTargets)
        {
            string result =
                "visuals="
                + visualCount
                + " | missingSources="
                + missingSources.Count
                + " | missingTargets="
                + missingTargets.Count;

            if (missingSources.Count > 0)
            {
                result +=
                    " | source="
                    + string.Join(
                        ", ",
                        missingSources.ToArray());
            }

            if (missingTargets.Count > 0)
            {
                result +=
                    " | target="
                    + string.Join(
                        ", ",
                        missingTargets.ToArray());
            }

            return result;
        }

        private sealed class VisualBinding
        {
            internal string SourceName;
            internal string CenterJoint;
            internal string RightJoint;
            internal string LeftJoint;
            internal bool IsMirroredPair;

            internal static VisualBinding Center(
                string sourceName,
                string centerJoint)
            {
                return new VisualBinding
                {
                    SourceName = sourceName,
                    CenterJoint = centerJoint
                };
            }

            internal static VisualBinding MirroredPair(
                string sourceName,
                string rightJoint,
                string leftJoint)
            {
                return new VisualBinding
                {
                    SourceName = sourceName,
                    RightJoint = rightJoint,
                    LeftJoint = leftJoint,
                    IsMirroredPair = true
                };
            }
        }
    }
}
