using System.Collections.Generic;
using UnityEngine;

namespace PB_AnimationLibrary
{
    internal static class PlanningPreviewRendererRefresh
    {
        private sealed class RendererState
        {
            internal SkinnedMeshRenderer Renderer;
            internal bool ForceMatrixRecalculationPerRender;
        }

        private static readonly List<RendererState> states =
            new List<RendererState>();

        private static readonly List<Transform> roots =
            new List<Transform>();

        internal static void Enable(Transform root)
        {
            if (root == null || roots.Contains(root))
                return;

            roots.Add(root);

            SkinnedMeshRenderer[] renderers =
                root.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            for (int i = 0; i < renderers.Length; ++i)
            {
                SkinnedMeshRenderer renderer = renderers[i];
                if (renderer == null ||
                    ContainsRenderer(renderer))
                {
                    continue;
                }

                states.Add(
                    new RendererState
                    {
                        Renderer = renderer,
                        ForceMatrixRecalculationPerRender =
                            renderer.forceMatrixRecalculationPerRender
                    });

                // Planning에서는 render 직전 bone 변경이 skin matrix에 반영되도록 재계산 강제
                renderer.forceMatrixRecalculationPerRender = true;
            }
        }

        internal static void Restore()
        {
            for (int i = 0; i < states.Count; ++i)
            {
                RendererState state = states[i];
                if (state == null || state.Renderer == null)
                    continue;

                state.Renderer.forceMatrixRecalculationPerRender =
                    state.ForceMatrixRecalculationPerRender;
            }

            states.Clear();
            roots.Clear();
        }

        private static bool ContainsRenderer(
            SkinnedMeshRenderer renderer)
        {
            for (int i = 0; i < states.Count; ++i)
            {
                RendererState state = states[i];
                if (state != null &&
                    state.Renderer == renderer)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
