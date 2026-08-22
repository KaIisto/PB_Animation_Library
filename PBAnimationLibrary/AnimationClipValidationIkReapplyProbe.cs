using HarmonyLib;
using System;
using System.Reflection;

namespace PB_AnimationLibrary
{
    internal static class AnimationClipValidationIkReapplyProbe
    {
        private sealed class SolverAccessor
        {
            internal MemberInfo ComponentMember;
            internal MemberInfo SolverMember;
            internal MethodInfo UpdateMethod;
        }

        private static Type cachedViewType;
        private static SolverAccessor aimTorsoAccessor;
        private static SolverAccessor fullBodyAccessor;
        private static bool accessorsReady;
        private static bool accessorsFailed;
        private static bool successLogged;
        private static bool failureLogged;

        internal static bool TryApply(CombatEntity actor)
        {
            if (actor == null ||
                !actor.hasMechAnimationView ||
                actor.mechAnimationView.view == null)
            {
                LogFailureOnce("view-unavailable");
                return false;
            }

            object view = actor.mechAnimationView.view;
            if (!EnsureAccessors(view.GetType()))
                return false;

            try
            {
                bool aimApplied = InvokeSolverUpdate(view, aimTorsoAccessor);
                bool fullBodyApplied = InvokeSolverUpdate(view, fullBodyAccessor);

                if (!aimApplied || !fullBodyApplied)
                {
                    LogFailureOnce(
                        "solver-unavailable"
                        + "|aimTorso="
                        + aimApplied
                        + "|fullBody="
                        + fullBodyApplied);
                    return false;
                }

                if (!successLogged)
                {
                    successLogged = true;
                    AnimationLibraryLog.Info(
                        "AnimationValidation|IK_REAPPLY_READY"
                        + "|actor="
                        + (actor.hasId ? actor.id.id : -1)
                        + "|order=aim-torso,full-body");
                }

                return true;
            }
            catch (Exception exception)
            {
                if (!failureLogged)
                {
                    failureLogged = true;
                    AnimationLibraryLog.Error(
                        "Animation validation IK reapply failed",
                        exception);
                }

                return false;
            }
        }

        private static bool EnsureAccessors(Type viewType)
        {
            if (viewType == null)
                return false;

            if (accessorsReady && cachedViewType == viewType)
                return true;

            if (accessorsFailed && cachedViewType == viewType)
                return false;

            cachedViewType = viewType;
            aimTorsoAccessor = CreateAccessor(viewType, "ikAimTorso");
            fullBodyAccessor = CreateAccessor(viewType, "ikFullBodyIK");
            accessorsReady = aimTorsoAccessor != null && fullBodyAccessor != null;
            accessorsFailed = !accessorsReady;

            if (!accessorsReady)
                LogFailureOnce("accessor-build-failed|viewType=" + viewType.FullName);

            return accessorsReady;
        }

        private static SolverAccessor CreateAccessor(
            Type viewType,
            string componentMemberName)
        {
            MemberInfo componentMember = FindInstanceMember(
                viewType,
                componentMemberName);

            Type componentType = GetMemberType(componentMember);
            if (componentType == null)
                return null;

            MemberInfo solverMember = FindInstanceMember(
                componentType,
                "solver");

            Type solverType = GetMemberType(solverMember);
            if (solverType == null)
                return null;

            MethodInfo updateMethod = AccessTools.Method(
                solverType,
                "Update",
                Type.EmptyTypes);

            if (updateMethod == null)
                return null;

            return new SolverAccessor
            {
                ComponentMember = componentMember,
                SolverMember = solverMember,
                UpdateMethod = updateMethod
            };
        }

        private static bool InvokeSolverUpdate(
            object view,
            SolverAccessor accessor)
        {
            if (view == null || accessor == null)
                return false;

            object component = ReadMember(view, accessor.ComponentMember);
            if (component == null)
                return false;

            object solver = ReadMember(component, accessor.SolverMember);
            if (solver == null)
                return false;

            accessor.UpdateMethod.Invoke(solver, null);
            return true;
        }

        private static MemberInfo FindInstanceMember(
            Type type,
            string memberName)
        {
            FieldInfo field = AccessTools.Field(type, memberName);
            if (field != null)
                return field;

            PropertyInfo property = AccessTools.Property(type, memberName);
            if (property == null || property.GetIndexParameters().Length != 0)
                return null;

            return property;
        }

        private static Type GetMemberType(MemberInfo member)
        {
            FieldInfo field = member as FieldInfo;
            if (field != null)
                return field.FieldType;

            PropertyInfo property = member as PropertyInfo;
            return property != null ? property.PropertyType : null;
        }

        private static object ReadMember(
            object instance,
            MemberInfo member)
        {
            FieldInfo field = member as FieldInfo;
            if (field != null)
                return field.GetValue(instance);

            PropertyInfo property = member as PropertyInfo;
            return property != null ? property.GetValue(instance, null) : null;
        }

        private static void LogFailureOnce(string reason)
        {
            if (failureLogged)
                return;

            failureLogged = true;
            AnimationLibraryLog.Warn(
                "AnimationValidation|IK_REAPPLY_UNAVAILABLE|reason="
                + reason);
        }
    }
}
