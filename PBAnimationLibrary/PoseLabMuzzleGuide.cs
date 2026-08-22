using System.Collections.Generic;
using UnityEngine;

namespace PB_AnimationLibrary
{
    internal sealed class PoseLabMuzzleGuide
    {
        private const float LineWidth = 0.025f;

        private readonly List<Transform> sources =
            new List<Transform>();

        private readonly List<Transform> sourceBuffer =
            new List<Transform>();

        private readonly List<GameObject> guideObjects =
            new List<GameObject>();

        private readonly List<LineRenderer> renderers =
            new List<LineRenderer>();

        private Material material;

        internal int Count
        {
            get { return sources.Count; }
        }

        internal void Update(
            CombatEntity actor,
            float length)
        {
            CollectSources(
                actor,
                sourceBuffer);

            if (sourceBuffer.Count == 0)
            {
                Clear();
                return;
            }

            if (!SourcesMatch(sourceBuffer) &&
                !Rebuild(sourceBuffer))
            {
                return;
            }

            for (int i = 0; i < renderers.Count; ++i)
            {
                UpdateGeometry(
                    renderers[i],
                    length);
            }
        }

        internal void Clear()
        {
            sources.Clear();
            renderers.Clear();

            for (int i = 0; i < guideObjects.Count; ++i)
            {
                GameObject guideObject =
                    guideObjects[i];

                if (guideObject != null)
                    Object.Destroy(guideObject);
            }

            guideObjects.Clear();

            if (material != null)
            {
                Object.Destroy(material);
                material = null;
            }
        }

        private static void CollectSources(
            CombatEntity actor,
            List<Transform> output)
        {
            output.Clear();

            if (actor == null ||
                !actor.hasMechAnimationView ||
                actor.mechAnimationView.view == null)
            {
                return;
            }

            Component viewComponent =
                actor.mechAnimationView.view as Component;

            if (viewComponent == null)
                return;

            UnitVisualManager visualManager =
                viewComponent.GetComponentInChildren<UnitVisualManager>(
                    true);

            if (visualManager == null)
                return;

            AddSocketSources(
                visualManager.GetSocketLink("equipment_left"),
                output);

            AddSocketSources(
                visualManager.GetSocketLink("equipment_right"),
                output);
        }

        private static void AddSocketSources(
            UnitSocketVisual socketVisual,
            List<Transform> output)
        {
            if (socketVisual == null ||
                socketVisual.activationLinks == null)
            {
                return;
            }

            for (int i = 0;
                i < socketVisual.activationLinks.Count;
                ++i)
            {
                ItemActivationLink link =
                    socketVisual.activationLinks[i];

                Transform source =
                    link != null
                        ? link.visualTransform
                        : null;

                if (source == null ||
                    output.Contains(source))
                {
                    continue;
                }

                output.Add(source);
            }
        }

        private bool SourcesMatch(
            List<Transform> requestedSources)
        {
            if (sources.Count != requestedSources.Count)
                return false;

            for (int i = 0; i < requestedSources.Count; ++i)
            {
                if (sources[i] != requestedSources[i])
                    return false;
            }

            return true;
        }

        private bool Rebuild(
            List<Transform> requestedSources)
        {
            Clear();

            Shader shader =
                Shader.Find("Sprites/Default");

            if (shader == null)
                shader = Shader.Find("Hidden/Internal-Colored");

            if (shader == null)
            {
                AnimationLibraryLog.Warn(
                    "PoseLab muzzle guide unavailable"
                    + "|reason=shader_unavailable");

                return false;
            }

            material = new Material(shader);
            material.hideFlags = HideFlags.HideAndDontSave;

            Color guideColor =
                new Color(
                    1f,
                    0.65f,
                    0.15f,
                    1f);

            for (int i = 0; i < requestedSources.Count; ++i)
            {
                Transform source =
                    requestedSources[i];

                if (source == null)
                    continue;

                GameObject guideObject =
                    new GameObject(
                        "PBAnimationLibrary_MuzzleGuide");

                guideObject.hideFlags =
                    HideFlags.HideAndDontSave;

                guideObject.layer =
                    source.gameObject.layer;

                Transform guideTransform =
                    guideObject.transform;

                guideTransform.SetParent(
                    source,
                    false);

                LineRenderer renderer =
                    guideObject.AddComponent<LineRenderer>();

                renderer.useWorldSpace = false;
                renderer.loop = false;
                renderer.startWidth = LineWidth;
                renderer.endWidth = LineWidth;
                renderer.numCapVertices = 2;
                renderer.numCornerVertices = 2;
                renderer.sharedMaterial = material;
                renderer.startColor = guideColor;
                renderer.endColor = guideColor;

                sources.Add(source);
                guideObjects.Add(guideObject);
                renderers.Add(renderer);
            }

            return sources.Count > 0;
        }

        private static void UpdateGeometry(
            LineRenderer renderer,
            float length)
        {
            if (renderer == null)
                return;

            float arrowLength =
                Mathf.Min(
                    0.55f,
                    length * 0.22f);

            float arrowWidth =
                arrowLength * 0.55f;

            Vector3 tip =
                Vector3.forward * length;

            Vector3 arrowBack =
                Vector3.forward *
                (length - arrowLength);

            Vector3 left =
                arrowBack +
                Vector3.left * arrowWidth;

            Vector3 right =
                arrowBack +
                Vector3.right * arrowWidth;

            renderer.positionCount = 6;
            renderer.SetPosition(0, Vector3.zero);
            renderer.SetPosition(1, tip);
            renderer.SetPosition(2, left);
            renderer.SetPosition(3, tip);
            renderer.SetPosition(4, right);
            renderer.SetPosition(5, tip);
        }
    }
}
