using HarmonyLib;
using PhantomBrigade.Mods;

namespace PB_AnimationLibrary
{
    public sealed class ModLinkCustom : ModLink
    {
        public const string ReleaseVersion = "0.11.3-release-cleanup1";

        public override void OnLoad(Harmony harmonyInstance)
        {
            AnimationLibraryInstaller.Install(harmonyInstance);
            base.OnLoad(harmonyInstance);

            AnimationLibraryLog.Info(
                "Loaded"
                + " | version="
                + ReleaseVersion
                + " | mod="
                + modID);
        }
    }
}
