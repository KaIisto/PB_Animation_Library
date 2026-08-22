using System;
using System.Collections.Generic;
using UnityEngine;

namespace PB_AnimationLibrary
{
    internal enum AnimationClipValidationHeadPolicy
    {
        PreserveCurrentHead = 0,
        ClipOwnsHead = 1
    }

    internal static class AnimationClipValidationRuntime
    {
        private sealed class PlaybackState
        {
            internal CombatEntity Actor;
            internal string AnimationId;
            internal AnimationClip Clip;
            internal PoseSnapshot RestorePose;
            internal bool Loop;
            internal bool Playing;
            internal float Time;
            internal AnimationClipValidationHeadPolicy HeadPolicy;
            internal int LastAdvanceFrame = -1;
            internal int LastMechLateUpdateFrame = -1;
            internal bool FirstSampleLogged;
        }

        private static readonly Dictionary<int, PlaybackState> states =
            new Dictionary<int, PlaybackState>();

        internal static bool Start(
            CombatEntity actor,
            AnimationClipValidationEntry entry,
            bool loop,
            AnimationClipValidationHeadPolicy headPolicy,
            out string status)
        {
            status = string.Empty;

            int actorId;
            if (!TryGetActorId(
                    actor,
                    out actorId))
            {
                status = "Start failed: actor is unavailable.";
                return false;
            }

            if (entry == null ||
                entry.Clip == null)
            {
                status = "Start failed: AnimationClip validation entry is missing.";
                return false;
            }

            string animationId =
                entry.DisplayId;

            AnimationClip clip =
                entry.Clip;

            Transform samplingRoot;
            Transform jointRoot;
            if (!VisibleMechRigResolver.TryResolve(
                    actor,
                    out samplingRoot,
                    out jointRoot))
            {
                status = "Start failed: visible mech rig is unavailable.";
                return false;
            }

            Stop(
                actor,
                true);

            PoseSnapshot restorePose;
            if (!PoseSnapshotCapture.TryCapture(
                    actor,
                    out restorePose))
            {
                status = "Start failed: current pose could not be captured.";
                return false;
            }

            PlaybackState state =
                new PlaybackState
                {
                    Actor = actor,
                    AnimationId = animationId,
                    Clip = clip,
                    RestorePose = restorePose,
                    Loop = loop,
                    Playing = true,
                    Time = 0f,
                    HeadPolicy = headPolicy
                };

            states[actorId] =
                state;

            if (!ApplySample(
                    state,
                    "start"))
            {
                states.Remove(
                    actorId);

                status = "Start failed while sampling AnimationClip.";
                return false;
            }

            status =
                "Playing "
                + animationId
                + " -> "
                + clip.name
                + " | length="
                + clip.length.ToString("F3")
                + " | loop="
                + loop
                + " | head="
                + GetHeadPolicyLabel(
                    headPolicy);

            AnimationLibraryLog.Info(
                "AnimationValidation|PLAY"
                + "|actor="
                + actorId
                + "|id="
                + animationId
                + "|bundle="
                + entry.BundleName
                + "|clip="
                + clip.name
                + "|legacy="
                + clip.legacy
                + "|length="
                + clip.length.ToString("F3")
                + "|frameRate="
                + clip.frameRate.ToString("F0")
                + "|loop="
                + loop
                + "|headPolicy="
                + GetHeadPolicyLabel(
                    headPolicy)
                + "|sampleStage=post-lateupdate");

            return true;
        }

        internal static bool Stop(
            CombatEntity actor,
            bool restore)
        {
            int actorId;
            if (!TryGetActorId(
                    actor,
                    out actorId))
            {
                return false;
            }

            PlaybackState state;
            if (!states.TryGetValue(
                    actorId,
                    out state) ||
                state.Actor != actor)
            {
                return false;
            }

            states.Remove(
                actorId);

            int restoredNodeCount = 0;
            if (restore &&
                state.RestorePose != null)
            {
                PoseSnapshotApply.TryApply(
                    actor,
                    state.RestorePose,
                    out restoredNodeCount);
            }

            AnimationLibraryLog.Info(
                "AnimationValidation|STOP"
                + "|actor="
                + actorId
                + "|id="
                + (state.AnimationId ?? string.Empty)
                + "|clip="
                + (state.Clip != null
                    ? state.Clip.name
                    : "(null)")
                + "|restored="
                + restore
                + "|restoredNodes="
                + restoredNodeCount);

            return true;
        }

        internal static void StopAll()
        {
            List<CombatEntity> actors =
                new List<CombatEntity>();

            foreach (PlaybackState state in states.Values)
            {
                if (state != null &&
                    state.Actor != null)
                {
                    actors.Add(
                        state.Actor);
                }
            }

            for (int i = 0; i < actors.Count; ++i)
            {
                Stop(
                    actors[i],
                    true);
            }

            states.Clear();
        }

        internal static bool SetPlaying(
            CombatEntity actor,
            bool playing)
        {
            PlaybackState state;
            if (!TryGetState(
                    actor,
                    out state))
            {
                return false;
            }

            state.Playing =
                playing;

            return true;
        }

        internal static bool SetTime(
            CombatEntity actor,
            float time)
        {
            PlaybackState state;
            if (!TryGetState(
                    actor,
                    out state) ||
                state.Clip == null)
            {
                return false;
            }

            state.Time =
                Mathf.Clamp(
                    time,
                    0f,
                    Mathf.Max(
                        0f,
                        state.Clip.length));

            state.Playing = false;

            return ApplySample(
                state,
                "scrub");
        }

        internal static bool TryGetStatus(
            CombatEntity actor,
            out string animationId,
            out float time,
            out float length,
            out bool playing,
            out bool loop,
            out AnimationClipValidationHeadPolicy headPolicy)
        {
            animationId = null;
            time = 0f;
            length = 0f;
            playing = false;
            loop = false;
            headPolicy =
                AnimationClipValidationHeadPolicy.PreserveCurrentHead;

            PlaybackState state;
            if (!TryGetState(
                    actor,
                    out state) ||
                state.Clip == null)
            {
                return false;
            }

            animationId =
                state.AnimationId;

            time =
                state.Time;

            length =
                state.Clip.length;

            playing =
                state.Playing;

            loop =
                state.Loop;

            headPolicy =
                state.HeadPolicy;

            return true;
        }

        internal static bool NeedsRenderFallback(
            CombatEntity actor)
        {
            PlaybackState state;
            return TryGetState(
                       actor,
                       out state) &&
                   state.Clip != null &&
                   state.LastMechLateUpdateFrame !=
                   Time.frameCount;
        }

        internal static void ApplyFromMechLateUpdate(
            CombatEntity actor,
            float deltaTime)
        {
            PlaybackState state;
            if (!TryGetState(
                    actor,
                    out state) ||
                state.Clip == null)
            {
                return;
            }

            state.LastMechLateUpdateFrame =
                Time.frameCount;

            ApplyFrame(
                state,
                Mathf.Max(
                    0f,
                    deltaTime),
                "mech-lateupdate");
        }

        internal static void ApplyFromRenderFallback(
            CombatEntity actor)
        {
            PlaybackState state;
            if (!TryGetState(
                    actor,
                    out state) ||
                state.Clip == null ||
                state.LastMechLateUpdateFrame ==
                Time.frameCount)
            {
                return;
            }

            ApplyFrame(
                state,
                Mathf.Max(
                    0f,
                    Time.unscaledDeltaTime),
                "render-precull");
        }

        private static void ApplyFrame(
            PlaybackState state,
            float deltaTime,
            string driver)
        {
            if (state == null ||
                state.Clip == null)
            {
                return;
            }

            AdvanceTime(
                state,
                deltaTime);

            ApplySample(
                state,
                driver);
        }

        private static void AdvanceTime(
            PlaybackState state,
            float deltaTime)
        {
            if (!state.Playing ||
                state.LastAdvanceFrame ==
                Time.frameCount)
            {
                return;
            }

            state.LastAdvanceFrame =
                Time.frameCount;

            float length =
                Mathf.Max(
                    0f,
                    state.Clip.length);

            if (length <= 0f)
            {
                state.Time = 0f;
                state.Playing = false;
                return;
            }

            float nextTime =
                state.Time +
                deltaTime;

            if (state.Loop)
            {
                state.Time =
                    Mathf.Repeat(
                        nextTime,
                        length);

                return;
            }

            if (nextTime >= length)
            {
                state.Time =
                    length;

                // One-shot은 마지막 pose를 유지하고 Stop 요청에서만 원래 pose로 복원
                state.Playing = false;
                return;
            }

            state.Time =
                nextTime;
        }

        private static bool ApplySample(
            PlaybackState state,
            string driver)
        {
            if (state == null ||
                state.Actor == null ||
                state.Clip == null)
            {
                return false;
            }

            Transform samplingRoot;
            Transform jointRoot;
            if (!VisibleMechRigResolver.TryResolve(
                    state.Actor,
                    out samplingRoot,
                    out jointRoot))
            {
                return false;
            }

            Transform head =
                state.HeadPolicy ==
                AnimationClipValidationHeadPolicy.PreserveCurrentHead
                    ? FindDescendantExact(
                        jointRoot,
                        "joint_head_xy")
                    : null;

            Vector3 headPosition =
                Vector3.zero;

            Quaternion headRotation =
                Quaternion.identity;

            Vector3 headScale =
                Vector3.one;

            if (head != null)
            {
                // clip에 head curve가 있어도 기존 Animator/조준 결과를 보존
                headPosition =
                    head.localPosition;

                headRotation =
                    head.localRotation;

                headScale =
                    head.localScale;
            }

            try
            {
                state.Clip.SampleAnimation(
                    samplingRoot.gameObject,
                    state.Time);
            }
            catch (Exception exception)
            {
                AnimationLibraryLog.Error(
                    "Animation validation sample failed"
                    + "|actor="
                    + (state.Actor.hasId
                        ? state.Actor.id.id
                        : -1)
                    + "|clip="
                    + state.Clip.name,
                    exception);

                state.Playing = false;
                return false;
            }

            if (head != null)
            {
                head.localPosition =
                    headPosition;

                head.localRotation =
                    headRotation;

                head.localScale =
                    headScale;
            }

            if (!state.FirstSampleLogged)
            {
                state.FirstSampleLogged = true;

                AnimationLibraryLog.Info(
                    "AnimationValidation|FIRST_SAMPLE"
                    + "|actor="
                    + (state.Actor.hasId
                        ? state.Actor.id.id
                        : -1)
                    + "|id="
                    + (state.AnimationId ?? string.Empty)
                    + "|clip="
                    + state.Clip.name
                    + "|driver="
                    + driver
                    + "|time="
                    + state.Time.ToString("F3")
                    + "|headPolicy="
                    + GetHeadPolicyLabel(
                        state.HeadPolicy));
            }

            return true;
        }

        private static Transform FindDescendantExact(
            Transform root,
            string name)
        {
            if (root == null)
                return null;

            if (string.Equals(
                    root.name,
                    name,
                    StringComparison.Ordinal))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; ++i)
            {
                Transform found =
                    FindDescendantExact(
                        root.GetChild(i),
                        name);

                if (found != null)
                    return found;
            }

            return null;
        }

        private static string GetHeadPolicyLabel(
            AnimationClipValidationHeadPolicy policy)
        {
            return policy ==
                   AnimationClipValidationHeadPolicy.PreserveCurrentHead
                ? "preserve"
                : "clip";
        }

        private static bool TryGetState(
            CombatEntity actor,
            out PlaybackState state)
        {
            state = null;

            int actorId;
            if (!TryGetActorId(
                    actor,
                    out actorId))
            {
                return false;
            }

            if (!states.TryGetValue(
                    actorId,
                    out state))
            {
                return false;
            }

            return state.Actor == actor;
        }

        private static bool TryGetActorId(
            CombatEntity actor,
            out int actorId)
        {
            actorId = -1;

            if (actor == null ||
                !actor.hasId)
            {
                return false;
            }

            actorId =
                actor.id.id;

            return true;
        }
    }
}
