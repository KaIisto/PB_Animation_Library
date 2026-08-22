using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace PB_AnimationLibrary
{
    internal static class VisibleMechRigResolver
    {
        private const string SamplingRootName = "unit_mech_body";
        private const string JointRootName = "joint_root";
        private const string TorsoJointMemberName = "torsoJoint";
        private const string LeftWeaponReferenceMemberName = "lWeaponJointPalmLocal";
        private const string RightWeaponReferenceMemberName = "rWeaponJointPalmLocal";
        private const string LeftWeaponRootMemberName = "lWeaponTransform";
        private const string RightWeaponRootMemberName = "rWeaponTransform";

        internal static bool TryResolve(
            CombatEntity actor,
            out Transform samplingRoot,
            out Transform jointRoot)
        {
            samplingRoot = null;
            jointRoot = null;

            if (actor == null ||
                !actor.hasMechAnimationView ||
                actor.mechAnimationView.view == null)
            {
                return false;
            }

            object view = actor.mechAnimationView.view;
            Transform torso = ReadTransform(view, TorsoJointMemberName);
            if (torso == null)
                return false;

            samplingRoot = FindAncestorExact(torso, SamplingRootName);
            if (samplingRoot == null)
                return false;

            jointRoot = FindDirectChildExact(samplingRoot, JointRootName);
            return jointRoot != null;
        }

        internal static bool TryResolveWeaponTransforms(
            CombatEntity actor,
            bool leftSide,
            out Transform reference,
            out Transform weaponRoot)
        {
            reference = null;
            weaponRoot = null;

            if (actor == null ||
                !actor.hasMechAnimationView ||
                actor.mechAnimationView.view == null)
            {
                return false;
            }

            object view = actor.mechAnimationView.view;

            reference = ReadTransform(
                view,
                leftSide
                    ? LeftWeaponReferenceMemberName
                    : RightWeaponReferenceMemberName);

            weaponRoot = ReadTransform(
                view,
                leftSide
                    ? LeftWeaponRootMemberName
                    : RightWeaponRootMemberName);

            return reference != null &&
                   weaponRoot != null;
        }

        private static Transform ReadTransform(object instance, string memberName)
        {
            Type type = instance.GetType();
            FieldInfo field = AccessTools.Field(type, memberName);
            if (field != null)
                return field.GetValue(instance) as Transform;

            PropertyInfo property = AccessTools.Property(type, memberName);
            if (property == null || property.GetIndexParameters().Length != 0)
                return null;

            return property.GetValue(instance, null) as Transform;
        }

        private static Transform FindAncestorExact(Transform start, string name)
        {
            Transform current = start;
            while (current != null)
            {
                if (string.Equals(current.name, name, StringComparison.Ordinal))
                    return current;

                current = current.parent;
            }

            return null;
        }

        private static Transform FindDirectChildExact(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; ++i)
            {
                Transform child = parent.GetChild(i);
                if (string.Equals(child.name, name, StringComparison.Ordinal))
                    return child;
            }

            return null;
        }
    }
}
