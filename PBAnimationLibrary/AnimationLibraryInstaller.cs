using HarmonyLib;
using PhantomBrigade.Combat.Systems;
using System;
using System.Reflection;

namespace PB_AnimationLibrary
{
    internal static class AnimationLibraryInstaller
    {
        private static bool installed;

        internal static void Install(Harmony harmony)
        {
            if (installed)
                return;

            if (harmony == null)
            {
                AnimationLibraryLog.Warn(
                    "Install skipped | Harmony instance unavailable");
                return;
            }

            MethodInfo target = AccessTools.Method(
                typeof(MechAnimationSystem),
                "LateUpdateUnit",
                new[] { typeof(CombatEntity), typeof(float) });

            MethodInfo postfixMethod = AccessTools.Method(
                typeof(MechAnimationLateUpdatePatch),
                "Postfix");

            if (target == null || postfixMethod == null)
            {
                AnimationLibraryLog.Warn(
                    "Install failed | MechAnimationSystem.LateUpdateUnit target unavailable");
                return;
            }

            try
            {
                HarmonyMethod postfix =
                    new HarmonyMethod(postfixMethod)
                    {
                        // vanilla animation 이후 Pose Lab preview와 validation overlay를 적용
                        priority = Priority.Last
                    };

                harmony.Patch(target, postfix: postfix);

                installed = true;

                AnimationLibraryLog.Info(
                    "Installed"
                    + " | patch=MechAnimationSystem.LateUpdateUnit"
                    + " | postfixPriority=Last");
            }
            catch (Exception exception)
            {
                AnimationLibraryLog.Error(
                    "Install failed",
                    exception);
            }
        }

        private static class MechAnimationLateUpdatePatch
        {
            private static void Postfix(
                CombatEntity actor,
                float deltaTime)
            {
                PoseLabWindowRuntime.Observe(actor);
                PlanningPreviewRendererRefresh.Restore();
                PoseSourceRuntime.ApplyFromMechLateUpdate(actor);
                PoseOverrideRuntime.ApplyFromMechLateUpdate(actor);
                AnimationClipValidationRuntime.ApplyFromMechLateUpdate(
                    actor,
                    deltaTime);
                WeaponFollowRuntime.ApplyFromMechLateUpdate(actor);
            }
        }
    }
}
