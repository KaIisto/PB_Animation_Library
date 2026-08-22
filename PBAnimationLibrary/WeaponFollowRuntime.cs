using System.Collections.Generic;
using UnityEngine;

namespace PB_AnimationLibrary
{
    internal static class WeaponFollowRuntime
    {
        private enum SideMode
        {
            None,
            PreserveOffset,
            NativeAnchor
        }

        private sealed class SideBinding
        {
            internal Transform Reference;
            internal Transform TargetSpace;
            internal Transform WeaponRoot;

            internal Vector3 RelativePosition;
            internal Quaternion RelativeRotation;
            internal Vector3 RestoreLocalPosition;
            internal Quaternion RestoreLocalRotation;
            internal bool Valid
            {
                get
                {
                    return Reference != null &&
                           TargetSpace != null &&
                           WeaponRoot != null;
                }
            }
        }

        private struct NativeAnchorPose
        {
            internal bool Valid;
            internal Vector3 LocalPosition;
            internal Quaternion LocalRotation;
        }

        private sealed class ActorNativeAnchorBaseline
        {
            internal CombatEntity Actor;
            internal NativeAnchorPose Left;
            internal NativeAnchorPose Right;
        }

        private sealed class ActorWeaponFollowState
        {
            internal CombatEntity Actor;
            internal SideBinding Left;
            internal SideBinding Right;
            internal SideMode LeftMode;
            internal SideMode RightMode;
        }

        private static readonly Dictionary<int, ActorWeaponFollowState> states =
            new Dictionary<int, ActorWeaponFollowState>();

        private static readonly Dictionary<int, ActorNativeAnchorBaseline> nativeAnchorBaselines =
            new Dictionary<int, ActorNativeAnchorBaseline>();

        internal static void CaptureNativeAnchorBaseline(CombatEntity actor)
        {
            int actorId;
            if (!TryGetActorId(actor, out actorId))
                return;

            ActorNativeAnchorBaseline existing;
            if (nativeAnchorBaselines.TryGetValue(actorId, out existing) &&
                existing.Actor == actor)
            {
                return;
            }

            nativeAnchorBaselines[actorId] =
                new ActorNativeAnchorBaseline
                {
                    Actor = actor,
                    Left = CaptureNativeAnchorPose(actor, true),
                    Right = CaptureNativeAnchorPose(actor, false)
                };
        }

        internal static bool IsEnabledFor(CombatEntity actor)
        {
            ActorWeaponFollowState state;
            return TryGetState(actor, out state) &&
                   (state.LeftMode != SideMode.None ||
                    state.RightMode != SideMode.None);
        }

        internal static bool IsLeftEnabledFor(CombatEntity actor)
        {
            ActorWeaponFollowState state;
            return TryGetState(actor, out state) &&
                   state.LeftMode == SideMode.PreserveOffset;
        }

        internal static bool IsRightEnabledFor(CombatEntity actor)
        {
            ActorWeaponFollowState state;
            return TryGetState(actor, out state) &&
                   state.RightMode == SideMode.PreserveOffset;
        }

        internal static bool IsLeftNativeAnchorEnabledFor(
            CombatEntity actor)
        {
            ActorWeaponFollowState state;
            return TryGetState(actor, out state) &&
                   state.LeftMode == SideMode.NativeAnchor;
        }

        internal static bool IsRightNativeAnchorEnabledFor(
            CombatEntity actor)
        {
            ActorWeaponFollowState state;
            return TryGetState(actor, out state) &&
                   state.RightMode == SideMode.NativeAnchor;
        }

        internal static bool IsLeftReadyFor(CombatEntity actor)
        {
            ActorWeaponFollowState state;
            return TryGetState(actor, out state) &&
                   state.LeftMode != SideMode.None &&
                   state.Left != null &&
                   state.Left.Valid;
        }

        internal static bool IsRightReadyFor(CombatEntity actor)
        {
            ActorWeaponFollowState state;
            return TryGetState(actor, out state) &&
                   state.RightMode != SideMode.None &&
                   state.Right != null &&
                   state.Right.Valid;
        }

        internal static bool SetLeftEnabled(
            CombatEntity actor,
            bool value)
        {
            return SetSideMode(
                actor,
                true,
                value
                    ? SideMode.PreserveOffset
                    : SideMode.None);
        }

        internal static bool SetRightEnabled(
            CombatEntity actor,
            bool value)
        {
            return SetSideMode(
                actor,
                false,
                value
                    ? SideMode.PreserveOffset
                    : SideMode.None);
        }

        internal static bool SetLeftNativeAnchorEnabled(
            CombatEntity actor,
            bool value)
        {
            return SetSideMode(
                actor,
                true,
                value
                    ? SideMode.NativeAnchor
                    : SideMode.None);
        }

        internal static bool SetRightNativeAnchorEnabled(
            CombatEntity actor,
            bool value)
        {
            return SetSideMode(
                actor,
                false,
                value
                    ? SideMode.NativeAnchor
                    : SideMode.None);
        }

        internal static bool Rebind(CombatEntity actor)
        {
            ActorWeaponFollowState state;
            if (!TryGetState(actor, out state))
                return false;

            if (state.LeftMode != SideMode.None)
            {
                SideBinding previous = state.Left;
                state.Left =
                    CaptureSide(
                        actor,
                        true,
                        state.LeftMode);

                if (state.LeftMode == SideMode.NativeAnchor)
                {
                    PreserveRestorePose(
                        previous,
                        state.Left);

                    ApplySide(state.Left);
                }

                if (!state.Left.Valid)
                {
                    state.Left = null;
                    state.LeftMode = SideMode.None;
                }
            }

            if (state.RightMode != SideMode.None)
            {
                SideBinding previous = state.Right;
                state.Right =
                    CaptureSide(
                        actor,
                        false,
                        state.RightMode);

                if (state.RightMode == SideMode.NativeAnchor)
                {
                    PreserveRestorePose(
                        previous,
                        state.Right);

                    ApplySide(state.Right);
                }

                if (!state.Right.Valid)
                {
                    state.Right = null;
                    state.RightMode = SideMode.None;
                }
            }

            if (state.LeftMode == SideMode.None &&
                state.RightMode == SideMode.None)
            {
                RemoveState(actor);
            }

            return state.LeftMode != SideMode.None ||
                   state.RightMode != SideMode.None;
        }

        internal static void ApplyFromMechLateUpdate(CombatEntity actor)
        {
            Apply(actor);
        }

        internal static void Apply(CombatEntity actor)
        {
            ActorWeaponFollowState state;
            if (!TryGetState(actor, out state) ||
                (state.LeftMode == SideMode.None &&
                 state.RightMode == SideMode.None))
            {
                return;
            }

            Apply(state);
        }

        internal static void Disable(
            CombatEntity actor,
            bool logResult)
        {
            ActorWeaponFollowState state;
            if (!TryGetState(actor, out state))
                return;

            int actorId =
                actor.hasId
                    ? actor.id.id
                    : -1;

            RestoreSide(state.Left);
            RestoreSide(state.Right);
            RemoveState(actor);

            if (logResult)
            {
                AnimationLibraryLog.Info(
                    "PoseLab|WEAPON_PREVIEW_ALL_DISABLED"
                    + "|actor=" + actorId);
            }
        }

        internal static void DisableAllWithoutRestore()
        {
            states.Clear();
            nativeAnchorBaselines.Clear();
        }

        private static bool SetSideMode(
            CombatEntity actor,
            bool leftSide,
            SideMode mode)
        {
            if (mode == SideMode.None)
            {
                DisableSide(
                    actor,
                    leftSide,
                    true);

                return false;
            }

            ActorWeaponFollowState state =
                GetOrCreateState(actor);

            if (state == null)
                return false;

            Transform samplingRoot;
            Transform jointRoot;
            if (!VisibleMechRigResolver.TryResolve(
                    actor,
                    out samplingRoot,
                    out jointRoot))
            {
                RemoveStateIfEmpty(actor, state);
                return false;
            }

            SideBinding previous =
                leftSide
                    ? state.Left
                    : state.Right;

            SideMode previousMode =
                leftSide
                    ? state.LeftMode
                    : state.RightMode;

            if (previousMode != SideMode.None)
                RestoreSide(previous);

            SideBinding binding =
                CaptureSide(
                    actor,
                    leftSide,
                    mode);

            if (!binding.Valid)
            {
                AnimationLibraryLog.Warn(
                    "PoseLab weapon preview unavailable"
                    + "|actor="
                    + (actor.hasId ? actor.id.id : -1)
                    + "|side="
                    + (leftSide ? "left" : "right")
                    + "|mode="
                    + GetModeLogValue(mode));

                if (leftSide)
                {
                    state.Left = null;
                    state.LeftMode = SideMode.None;
                }
                else
                {
                    state.Right = null;
                    state.RightMode = SideMode.None;
                }

                RemoveStateIfEmpty(actor, state);
                return false;
            }

            if (leftSide)
            {
                state.Left = binding;
                state.LeftMode = mode;
            }
            else
            {
                state.Right = binding;
                state.RightMode = mode;
            }

            ApplySide(binding);

            AnimationLibraryLog.Info(
                "PoseLab|WEAPON_PREVIEW_SIDE_ENABLED"
                + "|actor="
                + (actor.hasId ? actor.id.id : -1)
                + "|side="
                + (leftSide ? "left" : "right")
                + "|mode="
                + GetModeLogValue(mode));

            return true;
        }

        private static void DisableSide(
            CombatEntity actor,
            bool leftSide,
            bool logResult)
        {
            ActorWeaponFollowState state;
            if (!TryGetState(actor, out state))
                return;

            SideMode mode =
                leftSide
                    ? state.LeftMode
                    : state.RightMode;

            if (mode == SideMode.None)
                return;

            int actorId =
                actor.hasId
                    ? actor.id.id
                    : -1;

            if (leftSide)
            {
                RestoreSide(state.Left);
                state.Left = null;
                state.LeftMode = SideMode.None;
            }
            else
            {
                RestoreSide(state.Right);
                state.Right = null;
                state.RightMode = SideMode.None;
            }

            RemoveStateIfEmpty(actor, state);

            if (logResult)
            {
                AnimationLibraryLog.Info(
                    "PoseLab|WEAPON_PREVIEW_SIDE_DISABLED"
                    + "|actor=" + actorId
                    + "|side="
                    + (leftSide ? "left" : "right")
                    + "|mode="
                    + GetModeLogValue(mode));
            }
        }

        private static void Apply(ActorWeaponFollowState state)
        {
            if (state.LeftMode != SideMode.None)
                ApplySide(state.Left);

            if (state.RightMode != SideMode.None)
                ApplySide(state.Right);
        }

        private static ActorWeaponFollowState GetOrCreateState(
            CombatEntity actor)
        {
            int actorId;
            if (!TryGetActorId(actor, out actorId))
                return null;

            ActorWeaponFollowState state;
            if (states.TryGetValue(actorId, out state) &&
                state.Actor == actor)
            {
                return state;
            }

            state =
                new ActorWeaponFollowState
                {
                    Actor = actor
                };

            states[actorId] = state;
            return state;
        }

        private static bool TryGetState(
            CombatEntity actor,
            out ActorWeaponFollowState state)
        {
            state = null;

            int actorId;
            if (!TryGetActorId(actor, out actorId))
                return false;

            if (!states.TryGetValue(actorId, out state))
                return false;

            return state.Actor == actor;
        }

        private static void RemoveStateIfEmpty(
            CombatEntity actor,
            ActorWeaponFollowState state)
        {
            if (state.LeftMode != SideMode.None ||
                state.RightMode != SideMode.None)
            {
                return;
            }

            RemoveState(actor);
        }

        private static void RemoveState(CombatEntity actor)
        {
            int actorId;
            if (TryGetActorId(actor, out actorId))
                states.Remove(actorId);
        }

        private static bool TryGetActorId(
            CombatEntity actor,
            out int actorId)
        {
            actorId = -1;

            if (actor == null || !actor.hasId)
                return false;

            actorId = actor.id.id;
            return true;
        }

        private static SideBinding CaptureSide(
            CombatEntity actor,
            bool leftSide,
            SideMode mode)
        {
            Transform reference;
            Transform weaponRoot;

            VisibleMechRigResolver.TryResolveWeaponTransforms(
                actor,
                leftSide,
                out reference,
                out weaponRoot);

            SideBinding binding =
                new SideBinding
                {
                    Reference = reference,
                    WeaponRoot = weaponRoot
                };

            if (reference == null || weaponRoot == null)
                return binding;

            if (mode == SideMode.NativeAnchor)
            {
                Transform palm = reference.parent;
                if (palm == null)
                    return binding;

                NativeAnchorPose anchorPose;
                if (!TryGetNativeAnchorPose(
                        actor,
                        leftSide,
                        out anchorPose))
                {
                    anchorPose =
                        new NativeAnchorPose
                        {
                            Valid = true,
                            LocalPosition = reference.localPosition,
                            LocalRotation = reference.localRotation
                        };
                }

                binding.TargetSpace = palm;
                binding.RelativePosition = anchorPose.LocalPosition;
                binding.RelativeRotation = anchorPose.LocalRotation;
            }
            else
            {
                binding.TargetSpace = reference;
                binding.RelativePosition =
                    reference.InverseTransformPoint(
                        weaponRoot.position);

                binding.RelativeRotation =
                    Quaternion.Inverse(reference.rotation) *
                    weaponRoot.rotation;
            }

            binding.RestoreLocalPosition =
                weaponRoot.localPosition;

            binding.RestoreLocalRotation =
                weaponRoot.localRotation;

            return binding;
        }

        private static NativeAnchorPose CaptureNativeAnchorPose(
            CombatEntity actor,
            bool leftSide)
        {
            Transform reference;
            Transform weaponRoot;
            if (!VisibleMechRigResolver.TryResolveWeaponTransforms(
                    actor,
                    leftSide,
                    out reference,
                    out weaponRoot) ||
                reference == null ||
                reference.parent == null)
            {
                return default(NativeAnchorPose);
            }

            Transform palm = reference.parent;

            if (leftSide)
            {
                // joint_left_weapon_local_xyz는 Source Pose에 포함되므로 고정 hardpoint의 authored grip offset을 기준으로 사용
                Transform canonical =
                    palm.Find("hardpoint_left_weapon_old");

                if (canonical != null)
                {
                    return new NativeAnchorPose
                    {
                        Valid = true,
                        LocalPosition = canonical.localPosition,
                        LocalRotation = canonical.localRotation
                    };
                }
            }

            return new NativeAnchorPose
            {
                Valid = true,
                LocalPosition = reference.localPosition,
                LocalRotation = reference.localRotation
            };
        }

        private static bool TryGetNativeAnchorPose(
            CombatEntity actor,
            bool leftSide,
            out NativeAnchorPose pose)
        {
            pose = default(NativeAnchorPose);

            int actorId;
            if (!TryGetActorId(actor, out actorId))
                return false;

            ActorNativeAnchorBaseline baseline;
            if (!nativeAnchorBaselines.TryGetValue(actorId, out baseline) ||
                baseline.Actor != actor)
            {
                return false;
            }

            pose = leftSide ? baseline.Left : baseline.Right;
            return pose.Valid;
        }

        private static void PreserveRestorePose(
            SideBinding previous,
            SideBinding current)
        {
            if (previous == null ||
                current == null ||
                previous.WeaponRoot == null ||
                previous.WeaponRoot != current.WeaponRoot)
            {
                return;
            }

            current.RestoreLocalPosition =
                previous.RestoreLocalPosition;

            current.RestoreLocalRotation =
                previous.RestoreLocalRotation;
        }

        private static void ApplySide(SideBinding binding)
        {
            if (binding == null || !binding.Valid)
                return;

            binding.WeaponRoot.position =
                binding.TargetSpace.TransformPoint(
                    binding.RelativePosition);

            binding.WeaponRoot.rotation =
                binding.TargetSpace.rotation *
                binding.RelativeRotation;
        }

        private static void RestoreSide(SideBinding binding)
        {
            if (binding == null ||
                binding.WeaponRoot == null)
            {
                return;
            }

            binding.WeaponRoot.localPosition =
                binding.RestoreLocalPosition;

            binding.WeaponRoot.localRotation =
                binding.RestoreLocalRotation;
        }

        private static string GetModeLogValue(SideMode mode)
        {
            switch (mode)
            {
                case SideMode.PreserveOffset:
                    return "preserve-offset";

                case SideMode.NativeAnchor:
                    return "native-anchor";

                default:
                    return "none";
            }
        }

    }
}
