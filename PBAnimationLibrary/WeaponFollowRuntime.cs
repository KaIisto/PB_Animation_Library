using System;
using System.Collections.Generic;
using UnityEngine;

namespace PB_AnimationLibrary
{
    internal static class WeaponFollowRuntime
    {
        private const string LeftReferenceName = "joint_left_weapon_local_xyz";
        private const string RightReferenceName = "joint_right_weapon_local_xyz";
        private const string LeftWeaponRootName = "joint_left_weapon";
        private const string RightWeaponRootName = "joint_right_weapon";

        private sealed class SideBinding
        {
            internal Transform Reference;
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
                           WeaponRoot != null;
                }
            }
        }

        private sealed class ActorWeaponFollowState
        {
            internal CombatEntity Actor;
            internal SideBinding Left;
            internal SideBinding Right;
            internal bool LeftEnabled;
            internal bool RightEnabled;
            internal int LastMechLateUpdateFrame = -1;
        }

        private static readonly Dictionary<int, ActorWeaponFollowState> states =
            new Dictionary<int, ActorWeaponFollowState>();

        internal static bool IsEnabledFor(CombatEntity actor)
        {
            ActorWeaponFollowState state;
            return TryGetState(actor, out state) &&
                   (state.LeftEnabled || state.RightEnabled);
        }

        internal static bool IsLeftEnabledFor(CombatEntity actor)
        {
            ActorWeaponFollowState state;
            return TryGetState(actor, out state) &&
                   state.LeftEnabled;
        }

        internal static bool IsRightEnabledFor(CombatEntity actor)
        {
            ActorWeaponFollowState state;
            return TryGetState(actor, out state) &&
                   state.RightEnabled;
        }

        internal static bool IsLeftReadyFor(CombatEntity actor)
        {
            ActorWeaponFollowState state;
            return TryGetState(actor, out state) &&
                   state.LeftEnabled &&
                   state.Left != null &&
                   state.Left.Valid;
        }

        internal static bool IsRightReadyFor(CombatEntity actor)
        {
            ActorWeaponFollowState state;
            return TryGetState(actor, out state) &&
                   state.RightEnabled &&
                   state.Right != null &&
                   state.Right.Valid;
        }

        internal static bool NeedsRenderFallback(CombatEntity actor)
        {
            ActorWeaponFollowState state;
            return TryGetState(actor, out state) &&
                   (state.LeftEnabled || state.RightEnabled) &&
                   state.LastMechLateUpdateFrame != Time.frameCount;
        }

        internal static bool SetLeftEnabled(
            CombatEntity actor,
            bool value)
        {
            return SetSideEnabled(
                actor,
                true,
                value);
        }

        internal static bool SetRightEnabled(
            CombatEntity actor,
            bool value)
        {
            return SetSideEnabled(
                actor,
                false,
                value);
        }

        internal static bool Rebind(CombatEntity actor)
        {
            ActorWeaponFollowState state;
            if (!TryGetState(actor, out state))
                return false;

            Transform samplingRoot;
            Transform jointRoot;
            if (!VisibleMechRigResolver.TryResolve(
                    actor,
                    out samplingRoot,
                    out jointRoot))
            {
                Disable(actor, false);
                return false;
            }

            if (state.LeftEnabled)
            {
                state.Left =
                    CaptureSide(
                        samplingRoot,
                        jointRoot,
                        LeftReferenceName,
                        LeftWeaponRootName);

                if (!state.Left.Valid)
                {
                    state.Left = null;
                    state.LeftEnabled = false;
                }
            }

            if (state.RightEnabled)
            {
                state.Right =
                    CaptureSide(
                        samplingRoot,
                        jointRoot,
                        RightReferenceName,
                        RightWeaponRootName);

                if (!state.Right.Valid)
                {
                    state.Right = null;
                    state.RightEnabled = false;
                }
            }

            state.LastMechLateUpdateFrame = -1;

            if (!state.LeftEnabled && !state.RightEnabled)
                RemoveState(actor);

            return state.LeftEnabled || state.RightEnabled;
        }

        internal static void ApplyFromMechLateUpdate(CombatEntity actor)
        {
            ActorWeaponFollowState state;
            if (!TryGetState(actor, out state) ||
                (!state.LeftEnabled && !state.RightEnabled))
            {
                return;
            }

            state.LastMechLateUpdateFrame = Time.frameCount;
            Apply(state);
        }

        internal static void Apply(CombatEntity actor)
        {
            ActorWeaponFollowState state;
            if (!TryGetState(actor, out state) ||
                (!state.LeftEnabled && !state.RightEnabled))
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
                    "PoseLab|WEAPON_FOLLOW_ALL_DISABLED"
                    + "|actor=" + actorId);
            }
        }

        internal static void DisableAllWithoutRestore()
        {
            states.Clear();
        }

        private static bool SetSideEnabled(
            CombatEntity actor,
            bool leftSide,
            bool value)
        {
            if (!value)
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

            SideBinding binding =
                CaptureSide(
                    samplingRoot,
                    jointRoot,
                    leftSide
                        ? LeftReferenceName
                        : RightReferenceName,
                    leftSide
                        ? LeftWeaponRootName
                        : RightWeaponRootName);

            if (!binding.Valid)
            {
                AnimationLibraryLog.Warn(
                    "PoseLab weapon follow unavailable"
                    + "|actor="
                    + (actor.hasId ? actor.id.id : -1)
                    + "|side="
                    + (leftSide ? "left" : "right"));

                RemoveStateIfEmpty(actor, state);
                return false;
            }

            if (leftSide)
            {
                if (state.LeftEnabled)
                    RestoreSide(state.Left);

                state.Left = binding;
                state.LeftEnabled = true;
            }
            else
            {
                if (state.RightEnabled)
                    RestoreSide(state.Right);

                state.Right = binding;
                state.RightEnabled = true;
            }

            state.LastMechLateUpdateFrame = -1;

            AnimationLibraryLog.Info(
                "PoseLab|WEAPON_FOLLOW_SIDE_ENABLED"
                + "|actor="
                + (actor.hasId ? actor.id.id : -1)
                + "|side="
                + (leftSide ? "left" : "right")
                + "|reference="
                + (leftSide
                    ? LeftReferenceName
                    : RightReferenceName));

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

            bool wasEnabled =
                leftSide
                    ? state.LeftEnabled
                    : state.RightEnabled;

            if (!wasEnabled)
                return;

            int actorId =
                actor.hasId
                    ? actor.id.id
                    : -1;

            if (leftSide)
            {
                RestoreSide(state.Left);
                state.Left = null;
                state.LeftEnabled = false;
            }
            else
            {
                RestoreSide(state.Right);
                state.Right = null;
                state.RightEnabled = false;
            }

            state.LastMechLateUpdateFrame = -1;
            RemoveStateIfEmpty(actor, state);

            if (logResult)
            {
                AnimationLibraryLog.Info(
                    "PoseLab|WEAPON_FOLLOW_SIDE_DISABLED"
                    + "|actor=" + actorId
                    + "|side="
                    + (leftSide ? "left" : "right"));
            }
        }

        private static void Apply(ActorWeaponFollowState state)
        {
            if (state.LeftEnabled)
                ApplySide(state.Left);

            if (state.RightEnabled)
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
            if (state.LeftEnabled || state.RightEnabled)
                return;

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
            Transform samplingRoot,
            Transform jointRoot,
            string referenceName,
            string weaponRootName)
        {
            Transform reference =
                FindDescendantExact(
                    samplingRoot,
                    referenceName);

            Transform weaponRoot =
                FindDirectChildExact(
                    jointRoot,
                    weaponRootName);

            SideBinding binding =
                new SideBinding
                {
                    Reference = reference,
                    WeaponRoot = weaponRoot
                };

            if (!binding.Valid)
                return binding;

            binding.RelativePosition =
                reference.InverseTransformPoint(
                    weaponRoot.position);

            binding.RelativeRotation =
                Quaternion.Inverse(reference.rotation) *
                weaponRoot.rotation;

            binding.RestoreLocalPosition =
                weaponRoot.localPosition;

            binding.RestoreLocalRotation =
                weaponRoot.localRotation;

            return binding;
        }

        private static void ApplySide(SideBinding binding)
        {
            if (binding == null || !binding.Valid)
                return;

            binding.WeaponRoot.position =
                binding.Reference.TransformPoint(
                    binding.RelativePosition);

            binding.WeaponRoot.rotation =
                binding.Reference.rotation *
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

        private static Transform FindDirectChildExact(
            Transform parent,
            string name)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; ++i)
            {
                Transform child = parent.GetChild(i);

                if (string.Equals(
                        child.name,
                        name,
                        StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }
    }
}
