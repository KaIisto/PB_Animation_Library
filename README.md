# PB Animation Library / Pose Lab


## 1. Overview

PB Animation Library is an **authoring toolchain for creating custom animations in Phantom Brigade**.

- **Pose Lab (Mod)**  
  Uses an actual mech pose as the base, lets you adjust its joints, create keyframes at multiple points in time, and export them as a `PoseSequence`.
- **PB Mod SDK Add-on (SDK)**  
  Converts the `.pbalibpose.json` exported by Pose Lab into Unity's native `AnimationClip` format (`.anim`), which can then be included in an AssetBundle for use by mods.


Workflow:

```text
Install Pose Lab mod
→ Select / capture Source Pose
→ Edit Authoring Bones
→ Create PoseSequence keyframes
→ Select Track Scope
→ Export .pbalibpose.json
→ PB Mod SDK Add-on
→ Bake native .anim
→ Export AssetBundle
```

Pose Lab limits editable joints based on vanilla `UnitVisualManager.primaryBones`.

- Default authoring bones: 21
- Finger bones: 40 (optionally shown with `Show finger joints (advanced)`)
- `*_auto` and non-primary helpers: not directly editable
- `joint_pelvis_xyz`: additionally supports adjusting the Pelvis **local Y height**

It also supports Weapon Follow, native hand weapon snap preview, weapon muzzle guides, left/right symmetry and branch mirroring, a unit forward guide, and sparse Track Scopes.

This project's responsibility ends at **creating `.anim` files that can be used correctly in Phantom Brigade**.  
How a finished `.anim` is used—such as which action plays it, when it starts or stops, or how aiming, blending, and cancellation are handled—is the responsibility of WFEF or other consumer/content mods. Pose Lab does not control those behaviors.

---

## 2. Installing the Add-on

The following folder inside this ZIP contains the PB Mod SDK Add-on:

```text
PBAnimationLibrary_SDK_Add-on/
└─ Assets/
   └─ Editor/
      └─ PBAnimationLibrary/
```

### Installation

1. Open your Phantom Brigade Mod SDK project.
2. Copy the
   `PBAnimationLibrary_SDK_Add-on/Assets/Editor/PBAnimationLibrary/`
   folder from this package, or download `PBAnimationLibrary_SDK_Add-on` from the Git release and extract it, then place it in:
   `Assets/Editor/PBAnimationLibrary/`
   inside your SDK project.
3. Wait for Unity to finish compiling the scripts.
4. Installation is complete when the following items appear in the top menu:

```text
Tools
└─ PB Animation Library
   ├─ Pose Sequence Baker
   └─ Animation Preview
```

### Optional assets for Preview

Baking `.anim` files does not require any additional vanilla assets.

To use the automatic Visual Preview in the SDK, the following assets must exist somewhere inside the SDK project:

- `unit_mech_body` asset — official redistribution permission has not yet been granted
- The official PB Mod SDK `armor_set_skeleton-replace` asset — https://wiki.braceyourselfgames.com/en/PhantomBrigade/Modding/official-mech-armor-modding (download link at the bottom of the page)

---

## 3. Usage

### A. Creating an animation in Pose Lab

1. Subscribe to **PB Pose Lab** on the Steam Workshop.
2. Enable the mod and enter any combat encounter.
3. When Pose Lab appears, select the main Actor you want to edit in the **Source Browser**.
4. Choose a Source Pose.
   - `Capture current visible pose`: captures the currently visible pose as the Source Pose
   - `Refresh loaded customization poses`: searches for loaded vanilla customization poses
   - Select the desired customization pose if needed
   - `Restore original pose`: restores the original pose captured when the Actor was first opened
5. In the **Pose Editor**, select joints from `Authoring Bones` and rotate them.
   - Left/right symmetric editing and mirroring are available
   - Use `Left/Right weapon follows current offset` to preserve the weapon's current hand-relative offset while posing
   - If the Source Pose does not hold the weapon, use `Snap left/right weapon to native hand reference` to preview it at the native hand attachment reference
   - Use `Show equipped weapon muzzle guides` to display the +Z firing direction from each equipped hand weapon's `ItemActivationLink.visualTransform`
   - Use `Show unit forward guide` if needed
   - Weapon snap/follow and muzzle guides are authoring-preview only; weapon roots and muzzle transforms are not exported as PoseSequence/.anim tracks
   - Finger joints are only shown when `Show finger joints (advanced)` is enabled
   - Selecting `joint_pelvis_xyz` allows height adjustment with `Pelvis Height — local Y only`
6. Click `Show Pose Sequence window` to open the Pose Sequence window.
7. Set the `Clip name`, `Frame rate`, and `Sequence length`.
8. Move the Time slider to the desired point, create the pose, then click:
   `Add / replace keyframe at current time`
9. Repeat as needed to create additional keyframes.
10. Select the desired `Bake track scope`.
11. Click `Export bake exchange JSON`.

The exported file is saved under the following folder inside the game's `Application.persistentDataPath`, and Pose Lab also displays the most recent export path.

Default path:

```text
AppData\LocalLow\Brace Yourself Games\Phantom Brigade\PBAnimationLibrary\PoseSequenceExports
```

Relative path:

```text
PBAnimationLibrary/PoseSequenceExports/
```

All Pose Lab windows can be hidden or shown again with **Numpad 5**.

### B. Baking to `.anim` in the SDK

1. In the PB Mod SDK, open:
   `Tools > PB Animation Library > Pose Sequence Baker`
2. Select the `.pbalibpose.json` created by Pose Lab.
3. Set the `Clip asset name`.
4. Set the `AssetBundle name`.
5. Check the `Output asset folder`.
6. Click `Bake .anim`.

`Overwrite existing .anim` is OFF by default.  
If a file with the same name already exists, the Add-on preserves it and adds a suffix such as `_001` or `_002`.

### C. Previewing in the SDK

The simplest method:

1. Select the generated `.anim` in the Project window.
2. Open `Tools > PB Animation Library > Animation Preview`.

If the required local rig/reference assets are available, the Add-on automatically prepares the preview rig and visual proxy.

When everything is working correctly, `Automatic preview READY` is displayed.

### D. Exporting as an AssetBundle

Use the `AssetBundle name` assigned to the `.anim` and export the bundle through the standard PB Mod SDK AssetBundle export process.

### E. Final in-game `.anim` validation

The `AnimationClip Validation` section at the bottom of Pose Lab's Pose Sequence window provides an internal validation tool for checking whether the generated `.anim` can be loaded and sampled correctly on an actual visible mech in Phantom Brigade.

1. Load the AssetBundle containing the `.anim` in-game.
2. Click `Refresh loaded .anim` in `AnimationClip Validation`.
3. Select the desired clip.
4. Use Play / Pause / scrub / Stop to verify the animation again.

This Validation feature is only for checking authoring output and is **not** a gameplay API for external mods.

---

Additional documentation:

- `Docs/PoseSequence_Bake_Exchange_Schema_v1_KO.md` — bake exchange schema
- `Docs/PB_Animation_Library_Project_Charter_KO.md` — project responsibility boundaries
- `Docs/PB_Animation_Library_Validation_KO.md` — runtime validation details
