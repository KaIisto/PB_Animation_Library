# PB Animation Library — PB Mod SDK Add-on

## 1. Installation

Copy the following folder into your Phantom Brigade Mod SDK project:

```text
Assets/
└─ Editor/
   └─ PBAnimationLibrary/
```

If you downloaded the Add-on separately from a Git release, extract it first and copy the included `PBAnimationLibrary` folder to:

```text
<Your PB Mod SDK Project>/Assets/Editor/PBAnimationLibrary/
```

Wait for Unity to finish compiling the scripts.

Installation is complete when the following menu appears:

```text
Tools
└─ PB Animation Library
   ├─ Pose Sequence Baker
   └─ Animation Preview
```

### Optional assets for Animation Preview

Baking `.anim` files does **not** require any additional vanilla assets.

To use the automatic visual preview, the following assets must exist somewhere in the SDK project:

- `unit_mech_body` asset  
  This is required as the preview skeleton source. Official redistribution permission has not yet been granted, so it is not included with this Add-on.
- Official PB Mod SDK `armor_set_skeleton-replace` asset  
  Available from the official mech armor modding guide:  
  https://wiki.braceyourselfgames.com/en/PhantomBrigade/Modding/official-mech-armor-modding

---

## 2. Usage

### A. Bake a Pose Lab export into `.anim`

1. Open:

```text
Tools > PB Animation Library > Pose Sequence Baker
```

2. Select the `.pbalibpose.json` exported from Pose Lab.
3. Set `Clip asset name`.
4. Set `AssetBundle name`.
5. Check `Output asset folder`.
6. Click `Bake .anim`.

`Overwrite existing .anim` is OFF by default.

If an `.anim` with the same name already exists, the Add-on preserves the existing file and creates a new one with a suffix such as:

```text
_001
_002
_003
```

Enable overwrite only when you intentionally want to replace an existing `.anim`.

The `AssetBundle name` field assigns the Unity AssetBundle label to the generated `.anim`.  
The actual AssetBundle build/export is handled through the normal Phantom Brigade Mod SDK workflow.

### B. Preview a generated `.anim`

The simplest workflow is:

1. Select the generated `.anim` in the Unity Project window.
2. Open:

```text
Tools > PB Animation Library > Animation Preview
```

If the required local rig/reference assets are available, the Add-on will automatically:

```text
Find unit_mech_body
→ Create a Transform-only preview rig
→ Validate AnimationClip binding paths
→ Find armor_set_skeleton-replace
→ Create the visual proxy
→ Sample the clip at time 0
→ Frame the preview in Scene View
```

When setup succeeds, the window displays:

```text
Automatic preview READY
```

Use the preview controls to scrub through the clip, step between frames, play/pause, and loop the animation.

### C. Manual preview setup

If automatic setup cannot find the correct assets, open `Advanced / Manual Setup`.

From there you can:

- assign a Preview Root manually
- create a Transform-only copy from the selected GameObject
- show skeleton/joint names
- assign the SDK reference model manually
- build or remove the Visual Proxy

The Visual Proxy is only an Editor preview layer. It does not modify the baked AnimationClip or its binding paths.
