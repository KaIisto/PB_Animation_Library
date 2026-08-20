using System;
using System.Collections.Generic;
using UnityEngine;

namespace PB_AnimationLibrary
{
    internal static class PoseLabWindowRuntime
    {
        private const int CandidatePruneIntervalFrames = 120;

        private static readonly List<CombatEntity> candidates =
            new List<CombatEntity>();

        private static PoseLabWorkspaceWindow window;
        private static int lastPruneFrame = -1;

        internal static IList<CombatEntity> Candidates
        {
            get { return candidates; }
        }

        internal static void Observe(CombatEntity candidate)
        {
            if (!IsAvailable(candidate))
                return;

            if (!candidates.Contains(candidate))
            {
                candidates.Add(candidate);
                candidates.Sort(CompareActorsById);
            }

            PruneCandidatesIfNeeded();

            if (window != null)
                return;

            Component viewComponent =
                candidate.mechAnimationView.view as Component;

            if (viewComponent == null)
                return;

            window =
                viewComponent.GetComponent<PoseLabWorkspaceWindow>();

            if (window == null)
            {
                window =
                    viewComponent.gameObject
                        .AddComponent<PoseLabWorkspaceWindow>();
            }
        }

        internal static void Release(PoseLabWorkspaceWindow released)
        {
            if (window != released)
                return;

            window = null;
            candidates.Clear();
            lastPruneFrame = -1;
        }

        private static void PruneCandidatesIfNeeded()
        {
            int frame = Time.frameCount;
            if (lastPruneFrame >= 0 &&
                frame - lastPruneFrame < CandidatePruneIntervalFrames)
            {
                return;
            }

            lastPruneFrame = frame;

            for (int i = candidates.Count - 1; i >= 0; --i)
            {
                if (!IsAvailable(candidates[i]))
                    candidates.RemoveAt(i);
            }
        }

        private static bool IsAvailable(CombatEntity candidate)
        {
            if (candidate == null)
                return false;

            Transform samplingRoot;
            Transform jointRoot;

            return VisibleMechRigResolver.TryResolve(
                candidate,
                out samplingRoot,
                out jointRoot);
        }

        private static int CompareActorsById(
            CombatEntity left,
            CombatEntity right)
        {
            int leftId =
                left != null && left.hasId
                    ? left.id.id
                    : int.MaxValue;

            int rightId =
                right != null && right.hasId
                    ? right.id.id
                    : int.MaxValue;

            return leftId.CompareTo(rightId);
        }
    }

    internal sealed class PoseLabWorkspaceWindow : MonoBehaviour
    {
        private sealed class ActorWorkspaceState
        {
            internal CombatEntity Actor;
            internal PoseSnapshot OriginalPose;
            internal PoseSnapshot SourcePose;
            internal string SourcePoseName = "none";
            internal string FilterText = string.Empty;
            internal string SelectedPath;
            internal Vector3 SelectedEuler;
            internal bool SymmetricEdit;
            internal bool ShowFingerJoints;
            internal PoseAuthoringBoneSet AuthoringBones;
            internal Vector2 SourcePoseScroll;
            internal Vector2 NodeScroll;
            internal Vector2 SequenceScroll;

            internal PoseSequence Sequence =
                new PoseSequence();

            internal float SequenceTime;
            internal int SelectedSequenceKeyframe = -1;
            internal string LastSequenceExportPath;
            internal string LastSequenceExportStatus;
        }

        private const int SourceWindowId = 0x5042414C;
        private const int EditorWindowId = 0x5042414D;
        private const int SequenceWindowId = 0x5042414E;

        private const float SourceWindowWidth = 430f;
        private const float EditorWindowWidth = 600f;
        private const float SequenceWindowWidth = 500f;
        private const float SequenceWindowHeight = 520f;
        private const float WindowDefaultHeight = 650f;
        private const float WindowMinHeight = 340f;
        private const float WindowCollapsedHeight = 28f;
        private const float ScreenMargin = 16f;
        private const float WindowGap = 12f;
        private const float ActorListHeight = 110f;
        private const float NodeListMinHeight = 150f;
        private const float NodeListMaxHeight = 280f;
        private const float SourcePoseListHeight = 300f;
        private const int MaxVisibleMatches = 100;
        private const float ForwardGuideMinLength = 1f;
        private const float ForwardGuideMaxLength = 12f;
        private const float ForwardGuideDefaultLength = 4f;
        private const float ForwardGuideLineWidth = 0.035f;
        private const float PelvisHeightMinOffset = -5f;
        private const float PelvisHeightMaxOffset = 5f;

        private Rect sourceWindowRect =
            new Rect(
                20f,
                40f,
                SourceWindowWidth,
                WindowDefaultHeight);

        private Rect editorWindowRect =
            new Rect(
                20f + SourceWindowWidth + WindowGap,
                40f,
                EditorWindowWidth,
                WindowDefaultHeight);

        private Rect sequenceWindowRect =
            new Rect(
                20f + SourceWindowWidth + WindowGap
                    + EditorWindowWidth + WindowGap,
                40f,
                SequenceWindowWidth,
                SequenceWindowHeight);

        private readonly Dictionary<int, ActorWorkspaceState> actorStates =
            new Dictionary<int, ActorWorkspaceState>();

        private CombatEntity actor;
        private PoseSnapshot originalPose;
        private PoseSnapshot sourcePose;
        private string sourcePoseName = "none";

        private string filterText = string.Empty;
        private Vector2 sourceContentScroll;
        private Vector2 editorContentScroll;
        private Vector2 actorScroll;
        private Vector2 sourcePoseScroll;
        private Vector2 nodeScroll;
        private Vector2 sequenceContentScroll;

        private string selectedPath;
        private Vector3 selectedEuler;
        private bool symmetricEdit;
        private bool showFingerJoints;
        private PoseAuthoringBoneSet authoringBones;
        private string authoringBonesStatus;
        private bool poseListExpanded = true;
        private bool nodeListExpanded = true;
        private bool sequenceWindowVisible;
        private bool sourceWindowCollapsed;
        private bool editorWindowCollapsed;
        private bool sequenceWindowCollapsed;
        private bool windowsVisible = true;

        private bool forwardGuideVisible;
        private bool forwardGuideInverted;
        private float forwardGuideLength =
            ForwardGuideDefaultLength;

        private GameObject forwardGuideObject;
        private LineRenderer forwardGuideRenderer;
        private Transform forwardGuideRoot;
        private Material forwardGuideMaterial;

        private PoseSequence sequence =
            new PoseSequence();

        private float sequenceTime;
        private int selectedSequenceKeyframe = -1;
        private PoseSequenceSampleResult lastSequenceSample;
        private string lastSequenceExportPath;
        private string lastSequenceExportStatus;

        private string validationClipFilter = "pbalib";
        private int validationClipIndex;
        private bool validationClipLoop;
        private bool validationClipPreserveHead = true;
        private bool validationCatalogInitialized;
        private readonly List<AnimationClipValidationEntry> validationClipMatches =
            new List<AnimationClipValidationEntry>();
        private string validationClipStatus;

        internal void SetActor(CombatEntity value)
        {
            if (actor == value)
                return;

            if (value != null)
            {
                Transform samplingRoot;
                Transform jointRoot;
                if (!VisibleMechRigResolver.TryResolve(
                        value,
                        out samplingRoot,
                        out jointRoot))
                {
                    AnimationLibraryLog.Warn(
                        "PoseLab actor selection failed"
                        + "|actor="
                        + (value.hasId ? value.id.id : -1)
                        + "|reason=visible_rig_unavailable");
                    return;
                }
            }

            SaveActiveActorState();
            actor = value;
            LoadActiveActorState();

            if (actor == null)
                return;

            EnsureAuthoringBoneSet();
            ApplyActorPreview(actor);
            UpdateForwardGuide();

            AnimationLibraryLog.Info(
                "PoseLab|ACTOR_SELECTED"
                + "|actor="
                + (actor.hasId ? actor.id.id : -1)
                + "|observedActors="
                + PoseLabWindowRuntime.Candidates.Count
                + "|retainedActors="
                + actorStates.Count);
        }

        private void OnEnable()
        {
            Camera.onPreCull += OnCameraPreCull;
        }

        private void OnDisable()
        {
            Camera.onPreCull -= OnCameraPreCull;
            PlanningPreviewRendererRefresh.Restore();
            DestroyForwardGuide();
        }

        private void OnCameraPreCull(Camera camera)
        {
            UpdateForwardGuide();

            IList<CombatEntity> candidates =
                PoseLabWindowRuntime.Candidates;

            for (int i = 0; i < candidates.Count; ++i)
            {
                CombatEntity candidate = candidates[i];
                if (candidate == null)
                    continue;

                if (!PoseSourceRuntime.NeedsRenderFallback(candidate) &&
                    !PoseOverrideRuntime.NeedsRenderFallback(candidate) &&
                    !AnimationClipValidationRuntime.NeedsRenderFallback(candidate) &&
                    !WeaponFollowRuntime.NeedsRenderFallback(candidate))
                {
                    continue;
                }

                Transform samplingRoot;
                Transform jointRoot;
                if (!VisibleMechRigResolver.TryResolve(
                        candidate,
                        out samplingRoot,
                        out jointRoot))
                {
                    continue;
                }

                PlanningPreviewRendererRefresh.Enable(samplingRoot);
                PoseSourceRuntime.Apply(candidate);
                PoseOverrideRuntime.Apply(candidate);
                AnimationClipValidationRuntime.ApplyFromRenderFallback(candidate);
                WeaponFollowRuntime.Apply(candidate);
            }
        }

        private void OnGUI()
        {
            HandleVisibilityHotkey();

            if (!windowsVisible)
                return;

            ClampWindowsToScreen();

            sourceWindowRect = GUI.Window(
                SourceWindowId,
                sourceWindowRect,
                DrawSourceWindow,
                "PB Animation Pose Lab - Source Browser");

            if (!windowsVisible)
                return;

            editorWindowRect = GUI.Window(
                EditorWindowId,
                editorWindowRect,
                DrawEditorWindow,
                "PB Animation Pose Lab - Pose Editor");

            if (!windowsVisible ||
                !sequenceWindowVisible)
            {
                return;
            }

            sequenceWindowRect = GUI.Window(
                SequenceWindowId,
                sequenceWindowRect,
                DrawSequenceWindow,
                "PB Animation Pose Lab - Pose Sequence");
        }

        private void HandleVisibilityHotkey()
        {
            Event currentEvent = Event.current;

            if (currentEvent == null ||
                currentEvent.type != EventType.KeyDown ||
                currentEvent.keyCode != KeyCode.Keypad5)
            {
                return;
            }

            windowsVisible = !windowsVisible;
            currentEvent.Use();

            AnimationLibraryLog.Info(
                "PoseLab|WINDOWS_VISIBILITY"
                + "|visible=" + windowsVisible
                + "|hotkey=Keypad5");
        }

        private void DrawSourceWindow(int windowId)
        {
            if (DrawWindowCollapseButton(
                    ref sourceWindowCollapsed,
                    sourceWindowRect))
            {
                return;
            }

            sourceContentScroll =
                GUILayout.BeginScrollView(
                    sourceContentScroll,
                    false,
                    true);

            if (GUILayout.Button("Hide all windows (Numpad 5)"))
            {
                windowsVisible = false;
            }

            GUILayout.Label(
                "Numpad 5 toggles all Pose Lab windows.");

            DrawActorSelector();

            if (actor == null)
            {
                GUILayout.Space(8f);
                GUILayout.Label(
                    "Select a mech actor before capturing a source pose.");

                GUILayout.EndScrollView();
                GUI.DragWindow(
                    new Rect(
                        0f,
                        0f,
                        sourceWindowRect.width,
                        24f));
                return;
            }

            GUILayout.Space(8f);
            DrawSourcePoseControls();

            if (sourcePose != null)
            {
                GUILayout.Space(6f);
                GUILayout.Label(
                    "Source: " + sourcePoseName
                    + " | nodes=" + sourcePose.Nodes.Count
                    + " | edited="
                    + PoseOverrideRuntime.GetOverrideCount(actor));
            }

            GUILayout.EndScrollView();

            GUI.DragWindow(
                new Rect(
                    0f,
                    0f,
                    sourceWindowRect.width,
                    24f));
        }

        private void DrawEditorWindow(int windowId)
        {
            if (DrawWindowCollapseButton(
                    ref editorWindowCollapsed,
                    editorWindowRect))
            {
                return;
            }

            editorContentScroll =
                GUILayout.BeginScrollView(
                    editorContentScroll,
                    false,
                    true);

            if (actor == null)
            {
                GUILayout.Label(
                    "Select a mech actor in Source Browser.");
            }
            else if (sourcePose == null)
            {
                GUILayout.Label(
                    "Actor "
                    + (actor.hasId ? actor.id.id : -1)
                    + " selected.");

                GUILayout.Space(6f);
                GUILayout.Label(
                    "Choose or capture a Source Pose in Source Browser.");
            }
            else
            {
                GUILayout.Label(
                    "Actor "
                    + (actor.hasId ? actor.id.id : -1)
                    + " | Source: " + sourcePoseName
                    + " | nodes=" + sourcePose.Nodes.Count
                    + " | edited="
                    + PoseOverrideRuntime.GetOverrideCount(actor));

                DrawPreviewOptions();

                if (GUILayout.Button(
                        sequenceWindowVisible
                            ? "Hide Pose Sequence window"
                            : "Show Pose Sequence window"))
                {
                    sequenceWindowVisible =
                        !sequenceWindowVisible;
                }

                if (GUILayout.Button("Reset all edits"))
                    ResetAllEdits();

                GUILayout.Space(6f);
                GUILayout.Label("Node filter");
                filterText =
                    GUILayout.TextField(
                        filterText ?? string.Empty);

                GUILayout.Label(
                    "Search uses the full path; the list shows only the final node name.");

                DrawNodeList();

                GUILayout.Space(8f);
                DrawSelectedNode();
            }

            GUILayout.EndScrollView();

            GUI.DragWindow(
                new Rect(
                    0f,
                    0f,
                    editorWindowRect.width,
                    24f));
        }

        private void DrawSequenceWindow(int windowId)
        {
            if (DrawWindowCollapseButton(
                    ref sequenceWindowCollapsed,
                    sequenceWindowRect))
            {
                return;
            }

            sequenceContentScroll =
                GUILayout.BeginScrollView(
                    sequenceContentScroll,
                    false,
                    true);

            if (actor == null)
            {
                GUILayout.Label(
                    "Select a mech actor in Source Browser.");

                GUILayout.EndScrollView();

                GUI.DragWindow(
                    new Rect(
                        0f,
                        0f,
                        sequenceWindowRect.width,
                        24f));
                return;
            }

            if (sourcePose == null)
            {
                GUILayout.Label(
                    "Choose or capture a Source Pose first.");

                GUILayout.EndScrollView();

                GUI.DragWindow(
                    new Rect(
                        0f,
                        0f,
                        sequenceWindowRect.width,
                        24f));
                return;
            }

            GUILayout.Label(
                "Actor "
                + (actor.hasId ? actor.id.id : -1)
                + " | Source: "
                + sourcePoseName);

            GUILayout.Label(
                "Keyframes: "
                + sequence.Keyframes.Count
                + " | current edits: "
                + PoseOverrideRuntime.GetOverrideCount(actor));

            GUILayout.Space(6f);

            GUILayout.Label("Clip name");

            sequence.Name =
                GUILayout.TextField(
                    sequence.Name ?? string.Empty);

            GUILayout.Label(
                "Frame rate: "
                + sequence.FrameRate.ToString("F0")
                + " FPS");

            sequence.FrameRate =
                Mathf.Round(
                    GUILayout.HorizontalSlider(
                        sequence.FrameRate,
                        1f,
                        120f));

            GUILayout.Label(
                "Sequence length: "
                + sequence.Length.ToString("F2")
                + " s");

            float newLength =
                GUILayout.HorizontalSlider(
                    sequence.Length,
                    0.10f,
                    10f);

            if (Mathf.Abs(
                    newLength -
                    sequence.Length) >
                0.0001f)
            {
                sequence.Length = newLength;
                sequenceTime =
                    Mathf.Clamp(
                        sequenceTime,
                        0f,
                        sequence.Length);
            }

            GUILayout.Label(
                "Time: "
                + sequenceTime.ToString("F3")
                + " s");

            float newTime =
                GUILayout.HorizontalSlider(
                    sequenceTime,
                    0f,
                    Mathf.Max(
                        0.01f,
                        sequence.Length));

            if (Mathf.Abs(
                    newTime -
                    sequenceTime) >
                0.0001f)
            {
                sequenceTime = newTime;
                ApplySequenceTime();
            }

            GUILayout.Label(
                "Interpolation: position=Lerp | rotation=Slerp");

            GUILayout.Label(
                "Scrubbing replaces unrecorded current edits with the sampled sequence pose.");

            GUILayout.Space(6f);

            GUILayout.Label(
                "Bake track scope");

            int requestedTrackScope =
                GUILayout.SelectionGrid(
                    (int)sequence.TrackScope,
                    PoseSequenceTrackScopeUtility.Labels,
                    2);

            if (requestedTrackScope >= 0 &&
                requestedTrackScope <
                PoseSequenceTrackScopeUtility.Labels.Length)
            {
                sequence.TrackScope =
                    (PoseSequenceTrackScope)requestedTrackScope;
            }

            GUILayout.Label(
                sequence.TrackScope ==
                PoseSequenceTrackScope.EditedNodes
                    ? "Exports every edited vanilla primary bone track."
                    : "Scope is applied after the vanilla primary bone authoring filter.");

            GUILayout.Space(6f);

            if (GUILayout.Button(
                    "Add / replace keyframe at current time"))
            {
                CaptureSequenceKeyframe();
            }

            GUI.enabled =
                selectedSequenceKeyframe >= 0 &&
                selectedSequenceKeyframe <
                sequence.Keyframes.Count;

            if (GUILayout.Button("Delete selected keyframe"))
                DeleteSelectedSequenceKeyframe();

            GUI.enabled = true;

            GUILayout.Space(6f);

            GUI.enabled =
                sequence.Keyframes.Count > 0;

            if (GUILayout.Button(
                    "Export bake exchange JSON"))
            {
                ExportSequenceForBaking();
            }

            GUI.enabled = true;

            if (!string.IsNullOrEmpty(
                    lastSequenceExportStatus))
            {
                GUILayout.Label(
                    lastSequenceExportStatus,
                    GetWrappedLabelStyle());
            }

            if (!string.IsNullOrEmpty(
                    lastSequenceExportPath))
            {
                GUILayout.Label(
                    "Last export:",
                    GetWrappedLabelStyle());

                GUILayout.Label(
                    lastSequenceExportPath,
                    GetWrappedLabelStyle());
            }

            GUILayout.Space(6f);
            GUILayout.Label("Keyframes");

            for (int i = 0;
                 i < sequence.Keyframes.Count;
                 ++i)
            {
                PoseSequenceKeyframe frame =
                    sequence.Keyframes[i];

                string label =
                    (i == selectedSequenceKeyframe
                        ? "> "
                        : "  ")
                    + i
                    + " | t="
                    + frame.Time.ToString("F3")
                    + " | nodes="
                    + frame.NodeCount;

                if (GUILayout.Button(label))
                {
                    selectedSequenceKeyframe = i;
                    sequenceTime = frame.Time;
                    ApplySequenceTime();
                }
            }

            GUILayout.Space(6f);

            if (lastSequenceSample != null)
            {
                GUILayout.Label(
                    "Sample: "
                    + lastSequenceSample.FromTime.ToString("F3")
                    + " -> "
                    + lastSequenceSample.ToTime.ToString("F3")
                    + " | t="
                    + lastSequenceSample.Factor.ToString("F3")
                    + " | nodes="
                    + lastSequenceSample.AppliedNodeCount);
            }

            GUILayout.Space(10f);
            DrawAnimationClipValidationControls();

            GUILayout.EndScrollView();

            GUI.DragWindow(
                new Rect(
                    0f,
                    0f,
                    sequenceWindowRect.width,
                    24f));
        }

        private void DrawAnimationClipValidationControls()
        {
            GUILayout.Label(
                "AnimationClip Validation");

            if (!validationCatalogInitialized)
                RefreshAnimationClipValidationCatalog();

            GUILayout.BeginHorizontal();

            GUILayout.Label(
                "Filter",
                GUILayout.Width(44f));

            string requestedFilter =
                GUILayout.TextField(
                    validationClipFilter ?? string.Empty);

            if (!string.Equals(
                    requestedFilter,
                    validationClipFilter,
                    StringComparison.Ordinal))
            {
                validationClipFilter =
                    requestedFilter;

                ApplyAnimationClipValidationFilter();
            }

            if (GUILayout.Button(
                    "Refresh loaded .anim",
                    GUILayout.Width(130f)))
            {
                RefreshAnimationClipValidationCatalog();
            }

            GUILayout.EndHorizontal();

            GUILayout.Label(
                "Matches: "
                + validationClipMatches.Count
                + " | loaded="
                + AnimationClipValidationCatalog.Count);

            if (validationClipMatches.Count == 0)
            {
                GUILayout.Label(
                    "No loaded AnimationClip matched the filter.",
                    GetWrappedLabelStyle());

                if (!string.IsNullOrEmpty(validationClipStatus))
                {
                    GUILayout.Label(
                        validationClipStatus,
                        GetWrappedLabelStyle());
                }

                return;
            }

            validationClipIndex =
                Mathf.Clamp(
                    validationClipIndex,
                    0,
                    validationClipMatches.Count - 1);

            AnimationClipValidationEntry selected =
                validationClipMatches[validationClipIndex];

            GUILayout.Label(
                "Clip "
                + (validationClipIndex + 1)
                + " / "
                + validationClipMatches.Count
                + ": "
                + selected.Clip.name
                + " | bundle="
                + selected.BundleName,
                GetWrappedLabelStyle());

            GUILayout.Label(
                "Asset ID: "
                + selected.DisplayId,
                GetWrappedLabelStyle());

            GUILayout.Label(
                selected.AssetPath,
                GetWrappedLabelStyle());

            GUILayout.BeginHorizontal();

            GUI.enabled =
                validationClipMatches.Count > 1;

            if (GUILayout.Button("< Previous"))
            {
                validationClipIndex =
                    (validationClipIndex - 1 + validationClipMatches.Count) %
                    validationClipMatches.Count;
            }

            if (GUILayout.Button("Next >"))
            {
                validationClipIndex =
                    (validationClipIndex + 1) %
                    validationClipMatches.Count;
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            validationClipLoop =
                GUILayout.Toggle(
                    validationClipLoop,
                    "Loop");

            validationClipPreserveHead =
                GUILayout.Toggle(
                    validationClipPreserveHead,
                    "Preserve current head aim");

            string activeAnimationId;
            float activeTime;
            float activeLength;
            bool activePlaying;
            bool activeLoop;
            AnimationClipValidationHeadPolicy activeHeadPolicy;

            bool active =
                AnimationClipValidationRuntime.TryGetStatus(
                    actor,
                    out activeAnimationId,
                    out activeTime,
                    out activeLength,
                    out activePlaying,
                    out activeLoop,
                    out activeHeadPolicy);

            GUILayout.BeginHorizontal();

            GUI.enabled =
                actor != null &&
                selected != null &&
                selected.Clip != null;

            if (GUILayout.Button(
                    active
                        ? "Restart selected"
                        : "Play selected"))
            {
                AnimationClipValidationHeadPolicy headPolicy =
                    validationClipPreserveHead
                        ? AnimationClipValidationHeadPolicy.PreserveCurrentHead
                        : AnimationClipValidationHeadPolicy.ClipOwnsHead;

                string status;
                AnimationClipValidationRuntime.Start(
                    actor,
                    selected,
                    validationClipLoop,
                    headPolicy,
                    out status);

                validationClipStatus =
                    status;
            }

            GUI.enabled =
                active;

            if (GUILayout.Button(
                    active &&
                    activePlaying
                        ? "Pause"
                        : "Resume"))
            {
                if (activePlaying)
                    AnimationClipValidationRuntime.SetPlaying(actor, false);
                else
                    AnimationClipValidationRuntime.SetPlaying(actor, true);
            }

            if (GUILayout.Button("Stop"))
            {
                AnimationClipValidationRuntime.Stop(
                    actor,
                    true);

                validationClipStatus =
                    "Stopped and restored the pose captured at playback start.";
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (active)
            {
                GUILayout.Label(
                    "Active: "
                    + activeAnimationId
                    + " | "
                    + activeTime.ToString("F3")
                    + " / "
                    + activeLength.ToString("F3")
                    + " s"
                    + " | "
                    + (activePlaying
                        ? "playing"
                        : "holding"));

                float requestedTime =
                    GUILayout.HorizontalSlider(
                        activeTime,
                        0f,
                        Mathf.Max(
                            0.001f,
                            activeLength));

                if (Mathf.Abs(
                        requestedTime -
                        activeTime) >
                    0.0001f)
                {
                    AnimationClipValidationRuntime.SetTime(
                        actor,
                        requestedTime);
                }

                GUILayout.Label(
                    "Head policy: "
                    + (activeHeadPolicy ==
                       AnimationClipValidationHeadPolicy.PreserveCurrentHead
                        ? "preserve current head"
                        : "clip owns head")
                    + " | loop="
                    + activeLoop);
            }

            if (!string.IsNullOrEmpty(validationClipStatus))
            {
                GUILayout.Label(
                    validationClipStatus,
                    GetWrappedLabelStyle());
            }
        }

        private void RefreshAnimationClipValidationCatalog()
        {
            validationCatalogInitialized = true;

            int count =
                AnimationClipValidationCatalog.Refresh();

            ApplyAnimationClipValidationFilter();

            validationClipStatus =
                "Validation catalog refreshed | loaded="
                + count
                + " | matches="
                + validationClipMatches.Count;
        }

        private void ApplyAnimationClipValidationFilter()
        {
            AnimationClipValidationCatalog.FindMatches(
                validationClipFilter,
                validationClipMatches);

            validationClipIndex =
                validationClipMatches.Count > 0
                    ? Mathf.Clamp(
                        validationClipIndex,
                        0,
                        validationClipMatches.Count - 1)
                    : 0;
        }

        private void CaptureSequenceKeyframe()
        {
            PoseSequenceKeyframe keyframe;
            if (!EnsureAuthoringBoneSet() ||
                !PoseSequenceCapture.TryCapture(
                    actor,
                    sourcePose,
                    authoringBones,
                    sequenceTime,
                    out keyframe))
            {
                AnimationLibraryLog.Warn(
                    "PoseLab sequence capture failed"
                    + "|actor="
                    + (actor != null && actor.hasId
                        ? actor.id.id
                        : -1));
                return;
            }

            bool replaced;
            selectedSequenceKeyframe =
                sequence.AddOrReplace(
                    keyframe,
                    out replaced);

            AnimationLibraryLog.Info(
                "PoseLab|SEQUENCE_KEYFRAME_CAPTURED"
                + "|actor="
                + (actor.hasId ? actor.id.id : -1)
                + "|time="
                + keyframe.Time.ToString("F3")
                + "|nodes="
                + keyframe.NodeCount
                + "|replaced="
                + replaced);
        }

        private void ExportSequenceForBaking()
        {
            if (!EnsureAuthoringBoneSet())
            {
                string authoringError =
                    string.IsNullOrEmpty(authoringBonesStatus)
                        ? "authoring bone set is unavailable"
                        : authoringBonesStatus;

                lastSequenceExportPath = null;
                lastSequenceExportStatus =
                    "Export FAILED | "
                    + authoringError;

                AnimationLibraryLog.Warn(
                    "PoseLab sequence export failed"
                    + "|actor="
                    + (actor != null && actor.hasId
                        ? actor.id.id
                        : -1)
                    + "|keyframes="
                    + (sequence != null
                        ? sequence.Keyframes.Count
                        : 0)
                    + "|reason="
                    + authoringError);

                return;
            }

            string outputPath;
            int trackCount;
            int authoredTrackCount;
            int nonAuthoringExcludedTrackCount;
            bool overwritten;
            string error;

            if (!PoseSequenceBakeExchangeExporter.TryExport(
                    actor,
                    sourcePose,
                    authoringBones,
                    sourcePoseName,
                    sequence,
                    out outputPath,
                    out trackCount,
                    out authoredTrackCount,
                    out nonAuthoringExcludedTrackCount,
                    out overwritten,
                    out error))
            {
                string exportError =
                    string.IsNullOrEmpty(error)
                        ? "unknown error"
                        : error;

                lastSequenceExportPath = null;
                lastSequenceExportStatus =
                    "Export FAILED | "
                    + exportError;

                AnimationLibraryLog.Warn(
                    "PoseLab sequence export failed"
                    + "|actor="
                    + (actor != null && actor.hasId
                        ? actor.id.id
                        : -1)
                    + "|keyframes="
                    + (sequence != null
                        ? sequence.Keyframes.Count
                        : 0)
                    + "|reason="
                    + exportError);

                return;
            }

            lastSequenceExportPath =
                outputPath;

            lastSequenceExportStatus =
                "Export SUCCESS"
                + " | tracks="
                + trackCount
                + "/"
                + authoredTrackCount
                + " | scope="
                + PoseSequenceTrackScopeUtility.GetLabel(
                    sequence.TrackScope)
                + " | nonAuthoringExcluded="
                + nonAuthoringExcludedTrackCount
                + " | overwritten="
                + overwritten;

            AnimationLibraryLog.Info(
                "PoseLab|SEQUENCE_BAKE_EXPORTED"
                + "|actor="
                + (actor.hasId ? actor.id.id : -1)
                + "|clip="
                + sequence.Name
                + "|frameRate="
                + sequence.FrameRate.ToString("F0")
                + "|duration="
                + sequence.Length.ToString("F3")
                + "|keyframes="
                + sequence.Keyframes.Count
                + "|tracks="
                + trackCount
                + "|authoredTracks="
                + authoredTrackCount
                + "|nonAuthoringExcluded="
                + nonAuthoringExcludedTrackCount
                + "|scope="
                + PoseSequenceTrackScopeUtility.GetLabel(
                    sequence.TrackScope)
                + "|overwritten="
                + overwritten
                + "|path="
                + outputPath);
        }

        private void DeleteSelectedSequenceKeyframe()
        {
            if (!sequence.RemoveAt(
                    selectedSequenceKeyframe))
            {
                return;
            }

            if (sequence.Keyframes.Count == 0)
            {
                selectedSequenceKeyframe = -1;
                lastSequenceSample = null;
                return;
            }

            selectedSequenceKeyframe =
                Mathf.Clamp(
                    selectedSequenceKeyframe,
                    0,
                    sequence.Keyframes.Count - 1);

            sequenceTime =
                sequence.Keyframes[
                    selectedSequenceKeyframe].Time;

            ApplySequenceTime();

            AnimationLibraryLog.Info(
                "PoseLab|SEQUENCE_KEYFRAME_DELETED"
                + "|actor="
                + (actor.hasId ? actor.id.id : -1)
                + "|remaining="
                + sequence.Keyframes.Count);
        }

        private void ApplySequenceTime()
        {
            PoseSequenceSampleResult result;
            if (!EnsureAuthoringBoneSet() ||
                !PoseSequenceSampler.Apply(
                    actor,
                    sourcePose,
                    authoringBones,
                    sequence,
                    sequenceTime,
                    out result))
            {
                lastSequenceSample = null;
                return;
            }

            lastSequenceSample = result;
        }

        private void DrawActorSelector()
        {
            IList<CombatEntity> candidates =
                PoseLabWindowRuntime.Candidates;

            GUILayout.Label(
                "Mech Actor Selection ("
                + candidates.Count
                + ")");

            actorScroll =
                GUILayout.BeginScrollView(
                    actorScroll,
                    GUILayout.Height(ActorListHeight));

            for (int i = 0; i < candidates.Count; ++i)
            {
                CombatEntity candidate = candidates[i];
                if (candidate == null)
                    continue;

                int actorId =
                    candidate.hasId
                        ? candidate.id.id
                        : -1;

                string label =
                    (candidate == actor ? "> " : "  ")
                    + "Actor "
                    + actorId;

                if (GUILayout.Button(label))
                    SetActor(candidate);
            }

            GUILayout.EndScrollView();

            GUILayout.Label(
                actor != null
                    ? "Selected actor: "
                      + (actor.hasId ? actor.id.id : -1)
                    : "Selected actor: none");
        }

        private void DrawSourcePoseControls()
        {
            GUILayout.Label("Source Pose");

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Capture current visible pose"))
                CaptureCurrentVisibleSourcePose();

            GUI.enabled = originalPose != null;

            if (GUILayout.Button("Restore original pose"))
                RestoreOriginalPose();

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label(
                "Original pose: "
                + (originalPose != null
                    ? "captured"
                    : "not captured"));

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Refresh loaded customization poses"))
                VanillaPoseClipCatalog.Refresh();

            GUILayout.Label(
                VanillaPoseClipCatalog.HasScanned
                    ? "Loaded clips: "
                      + VanillaPoseClipCatalog.LoadedClipCount
                      + " | pose candidates: "
                      + VanillaPoseClipCatalog.CustomizationClips.Count
                    : "Runtime clip catalog not scanned");

            GUILayout.EndHorizontal();

            if (!VanillaPoseClipCatalog.HasScanned)
                return;

            if (VanillaPoseClipCatalog.CustomizationClips.Count == 0)
            {
                GUILayout.Label(
                    "No loaded customization_pose_* AnimationClips were found.");
                return;
            }

            string poseListLabel =
                (poseListExpanded ? "▼ " : "▶ ")
                + "Pose list ("
                + VanillaPoseClipCatalog.CustomizationClips.Count
                + ")";

            if (GUILayout.Button(poseListLabel))
                poseListExpanded = !poseListExpanded;

            if (!poseListExpanded)
                return;

            sourcePoseScroll =
                GUILayout.BeginScrollView(
                    sourcePoseScroll,
                    GUILayout.Height(SourcePoseListHeight));

            DrawCustomizationPoseGroup(
                "Symmetric Base Poses (_A)",
                VanillaPoseClipCatalog.SymmetricBaseClips);

            GUILayout.Space(6f);

            DrawCustomizationPoseGroup(
                "Other Customization Poses",
                VanillaPoseClipCatalog.OtherCustomizationClips);

            GUILayout.EndScrollView();
        }

        private void DrawCustomizationPoseGroup(
            string label,
            List<AnimationClip> clips)
        {
            GUILayout.Label(
                label + " (" + clips.Count + ")");

            for (int i = 0; i < clips.Count; ++i)
            {
                AnimationClip clip = clips[i];
                if (clip == null)
                    continue;

                if (GUILayout.Button(clip.name))
                    ApplyCustomizationPoseSource(clip);
            }
        }

        private void DrawPreviewOptions()
        {
            GUILayout.Space(6f);
            GUILayout.Label("Pose Preview");

            bool requestedForwardGuide =
                GUILayout.Toggle(
                    forwardGuideVisible,
                    "Show unit forward guide");

            if (requestedForwardGuide != forwardGuideVisible)
            {
                forwardGuideVisible =
                    requestedForwardGuide;

                UpdateForwardGuide();
            }

            if (forwardGuideVisible)
            {
                bool requestedInversion =
                    GUILayout.Toggle(
                        forwardGuideInverted,
                        "Invert guide direction (-Z)");

                if (requestedInversion != forwardGuideInverted)
                {
                    forwardGuideInverted =
                        requestedInversion;

                    UpdateForwardGuide();
                }

                GUILayout.Label(
                    "Guide length: "
                    + forwardGuideLength.ToString("F1"));

                float requestedLength =
                    GUILayout.HorizontalSlider(
                        forwardGuideLength,
                        ForwardGuideMinLength,
                        ForwardGuideMaxLength);

                if (Mathf.Abs(
                        requestedLength -
                        forwardGuideLength) >
                    0.001f)
                {
                    forwardGuideLength =
                        requestedLength;

                    UpdateForwardGuide();
                }

                GUILayout.Label(
                    forwardGuideInverted
                        ? "Root forward axis: -Z"
                        : "Root forward axis: +Z");
            }

            bool leftEnabled =
                WeaponFollowRuntime.IsLeftEnabledFor(actor);

            bool leftRequested =
                GUILayout.Toggle(
                    leftEnabled,
                    "Left weapon follows hand");

            if (leftRequested != leftEnabled)
            {
                WeaponFollowRuntime.SetLeftEnabled(
                    actor,
                    leftRequested);
            }

            bool rightEnabled =
                WeaponFollowRuntime.IsRightEnabledFor(actor);

            bool rightRequested =
                GUILayout.Toggle(
                    rightEnabled,
                    "Right weapon follows hand");

            if (rightRequested != rightEnabled)
            {
                WeaponFollowRuntime.SetRightEnabled(
                    actor,
                    rightRequested);
            }

            if (WeaponFollowRuntime.IsEnabledFor(actor))
            {
                GUILayout.Label(
                    "Weapon follow: left="
                    + (WeaponFollowRuntime.IsLeftEnabledFor(actor)
                        ? (WeaponFollowRuntime.IsLeftReadyFor(actor) ? "ready" : "missing")
                        : "off")
                    + " | right="
                    + (WeaponFollowRuntime.IsRightEnabledFor(actor)
                        ? (WeaponFollowRuntime.IsRightReadyFor(actor) ? "ready" : "missing")
                        : "off"));
            }
        }

        private void UpdateForwardGuide()
        {
            if (!forwardGuideVisible ||
                actor == null)
            {
                DestroyForwardGuide();
                return;
            }

            Transform samplingRoot;
            Transform jointRoot;

            if (!VisibleMechRigResolver.TryResolve(
                    actor,
                    out samplingRoot,
                    out jointRoot))
            {
                DestroyForwardGuide();
                return;
            }

            if (forwardGuideRoot != samplingRoot ||
                forwardGuideObject == null ||
                forwardGuideRenderer == null)
            {
                DestroyForwardGuide();

                if (!TryCreateForwardGuide(
                        samplingRoot))
                {
                    return;
                }
            }

            float direction =
                forwardGuideInverted
                    ? -1f
                    : 1f;

            float length =
                Mathf.Clamp(
                    forwardGuideLength,
                    ForwardGuideMinLength,
                    ForwardGuideMaxLength);

            float arrowLength =
                Mathf.Min(
                    0.55f,
                    length * 0.22f);

            float arrowWidth =
                arrowLength * 0.55f;

            Vector3 tip =
                Vector3.forward *
                (length * direction);

            Vector3 arrowBack =
                Vector3.forward *
                ((length - arrowLength) * direction);

            Vector3 left =
                arrowBack +
                Vector3.left * arrowWidth;

            Vector3 right =
                arrowBack +
                Vector3.right * arrowWidth;

            forwardGuideRenderer.positionCount = 6;
            forwardGuideRenderer.SetPosition(
                0,
                Vector3.zero);

            forwardGuideRenderer.SetPosition(
                1,
                tip);

            forwardGuideRenderer.SetPosition(
                2,
                left);

            forwardGuideRenderer.SetPosition(
                3,
                tip);

            forwardGuideRenderer.SetPosition(
                4,
                right);

            forwardGuideRenderer.SetPosition(
                5,
                tip);
        }

        private bool TryCreateForwardGuide(
            Transform samplingRoot)
        {
            Shader shader =
                Shader.Find(
                    "Sprites/Default");

            if (shader == null)
            {
                shader =
                    Shader.Find(
                        "Hidden/Internal-Colored");
            }

            if (shader == null)
            {
                AnimationLibraryLog.Warn(
                    "PoseLab forward guide unavailable"
                    + "|reason=shader_unavailable");

                return false;
            }

            forwardGuideMaterial =
                new Material(
                    shader);

            forwardGuideMaterial.hideFlags =
                HideFlags.HideAndDontSave;

            forwardGuideObject =
                new GameObject(
                    "PBAnimationLibrary_ForwardGuide");

            forwardGuideObject.hideFlags =
                HideFlags.HideAndDontSave;

            forwardGuideObject.layer =
                samplingRoot.gameObject.layer;

            Transform guideTransform =
                forwardGuideObject.transform;

            guideTransform.SetParent(
                samplingRoot,
                false);

            guideTransform.localPosition =
                Vector3.zero;

            guideTransform.localRotation =
                Quaternion.identity;

            guideTransform.localScale =
                Vector3.one;

            forwardGuideRenderer =
                forwardGuideObject.AddComponent<LineRenderer>();

            forwardGuideRenderer.useWorldSpace = false;
            forwardGuideRenderer.loop = false;
            forwardGuideRenderer.startWidth =
                ForwardGuideLineWidth;

            forwardGuideRenderer.endWidth =
                ForwardGuideLineWidth;

            forwardGuideRenderer.numCapVertices = 2;
            forwardGuideRenderer.numCornerVertices = 2;
            forwardGuideRenderer.material =
                forwardGuideMaterial;

            Color guideColor =
                new Color(
                    0.2f,
                    1f,
                    0.35f,
                    1f);

            forwardGuideRenderer.startColor =
                guideColor;

            forwardGuideRenderer.endColor =
                guideColor;

            forwardGuideRoot =
                samplingRoot;

            return true;
        }

        private void DestroyForwardGuide()
        {
            forwardGuideRoot = null;
            forwardGuideRenderer = null;

            if (forwardGuideObject != null)
            {
                Destroy(
                    forwardGuideObject);

                forwardGuideObject = null;
            }

            if (forwardGuideMaterial != null)
            {
                Destroy(
                    forwardGuideMaterial);

                forwardGuideMaterial = null;
            }
        }

        private void CaptureCurrentVisibleSourcePose()
        {
            if (!EnsureOriginalPose())
                return;

            PoseOverrideRuntime.ClearAll(actor);
            PlanningPreviewRendererRefresh.Restore();

            PoseSnapshot captured;
            if (!PoseSnapshotCapture.TryCapture(
                    actor,
                    out captured))
            {
                AnimationLibraryLog.Warn(
                    "Pose capture failed | visible mech rig unavailable");
                return;
            }

            sourcePose = captured;
            sourcePoseName = "Current Visible Pose";
            selectedPath = null;
            symmetricEdit = false;

            int boundNodeCount;
            if (!PoseSourceRuntime.SetSource(
                    actor,
                    sourcePose,
                    sourcePoseName,
                    out boundNodeCount))
            {
                AnimationLibraryLog.Warn(
                    "Pose source bind failed"
                    + " | source=current_visible");
                return;
            }

            PoseSourceRuntime.Apply(actor);

            if (WeaponFollowRuntime.IsEnabledFor(actor))
                WeaponFollowRuntime.Rebind(actor);

            if (!VanillaPoseClipCatalog.HasScanned)
                VanillaPoseClipCatalog.Refresh();

            AnimationLibraryLog.Info(
                "PoseLab|SOURCE_CAPTURED"
                + "|actor="
                + (actor != null && actor.hasId ? actor.id.id : -1)
                + "|source=current_visible"
                + "|nodes=" + sourcePose.Nodes.Count
                + "|boundNodes=" + boundNodeCount);
        }

        private bool EnsureOriginalPose()
        {
            if (originalPose != null)
                return true;

            PoseSnapshot captured;
            if (!PoseSnapshotCapture.TryCapture(
                    actor,
                    out captured))
            {
                AnimationLibraryLog.Warn(
                    "Original pose capture failed"
                    + " | visible mech rig unavailable");
                return false;
            }

            originalPose = captured;

            AnimationLibraryLog.Info(
                "PoseLab|ORIGINAL_POSE_CAPTURED"
                + "|actor="
                + (actor != null && actor.hasId ? actor.id.id : -1)
                + "|nodes=" + originalPose.Nodes.Count);

            return true;
        }

        private void ApplyCustomizationPoseSource(AnimationClip clip)
        {
            if (clip == null || !EnsureOriginalPose())
                return;

            PoseOverrideRuntime.ClearAll(actor);
            PoseSourceRuntime.Clear(actor);
            PlanningPreviewRendererRefresh.Restore();

            int restoredNodeCount;
            if (!PoseSnapshotApply.TryApply(
                    actor,
                    originalPose,
                    out restoredNodeCount))
            {
                AnimationLibraryLog.Warn(
                    "Customization source apply failed"
                    + " | reason=original_restore_failed"
                    + " | clip=" + clip.name);
                return;
            }

            Transform samplingRoot;
            Transform jointRoot;
            if (!VisibleMechRigResolver.TryResolve(
                    actor,
                    out samplingRoot,
                    out jointRoot))
            {
                AnimationLibraryLog.Warn(
                    "Customization source apply failed"
                    + " | reason=visible_rig_unavailable"
                    + " | clip=" + clip.name);
                return;
            }

            try
            {
                clip.SampleAnimation(
                    samplingRoot.gameObject,
                    0f);
            }
            catch (Exception exception)
            {
                PoseSnapshotApply.TryApply(
                    actor,
                    originalPose,
                    out restoredNodeCount);

                AnimationLibraryLog.Error(
                    "Customization source sample failed"
                    + " | clip=" + clip.name,
                    exception);
                return;
            }

            PoseSnapshot sampled;
            if (!PoseSnapshotCapture.TryCapture(
                    actor,
                    out sampled))
            {
                PoseSnapshotApply.TryApply(
                    actor,
                    originalPose,
                    out restoredNodeCount);

                AnimationLibraryLog.Warn(
                    "Customization source apply failed"
                    + " | reason=sample_capture_failed"
                    + " | clip=" + clip.name);
                return;
            }

            sourcePose = sampled;
            sourcePoseName = clip.name;
            selectedPath = null;
            symmetricEdit = false;

            int boundNodeCount;
            if (!PoseSourceRuntime.SetSource(
                    actor,
                    sourcePose,
                    sourcePoseName,
                    out boundNodeCount))
            {
                PoseSnapshotApply.TryApply(
                    actor,
                    originalPose,
                    out restoredNodeCount);

                sourcePose = originalPose;
                sourcePoseName = "Original Pose";

                AnimationLibraryLog.Warn(
                    "Customization source bind failed"
                    + " | clip=" + clip.name);
                return;
            }

            PoseSourceRuntime.Apply(actor);

            if (WeaponFollowRuntime.IsEnabledFor(actor))
                WeaponFollowRuntime.Rebind(actor);

            AnimationLibraryLog.Info(
                "PoseLab|CUSTOMIZATION_SOURCE_APPLIED"
                + "|actor="
                + (actor != null && actor.hasId ? actor.id.id : -1)
                + "|clip=" + clip.name
                + "|legacy=" + clip.legacy
                + "|length=" + clip.length.ToString("F4")
                + "|frameRate=" + clip.frameRate.ToString("F2")
                + "|sampleTime=0"
                + "|nodes=" + sourcePose.Nodes.Count
                + "|boundNodes=" + boundNodeCount);
        }

        private void RestoreOriginalPose()
        {
            if (originalPose == null)
                return;

            PoseOverrideRuntime.ClearAll(actor);
            PoseSourceRuntime.Clear(actor);
            PlanningPreviewRendererRefresh.Restore();

            int appliedNodeCount;
            if (!PoseSnapshotApply.TryApply(
                    actor,
                    originalPose,
                    out appliedNodeCount))
            {
                AnimationLibraryLog.Warn(
                    "Original pose restore failed");
                return;
            }

            sourcePose = originalPose;
            sourcePoseName = "Original Pose";
            selectedPath = null;
            symmetricEdit = false;

            if (WeaponFollowRuntime.IsEnabledFor(actor))
                WeaponFollowRuntime.Rebind(actor);

            AnimationLibraryLog.Info(
                "PoseLab|ORIGINAL_POSE_RESTORED"
                + "|actor="
                + (actor != null && actor.hasId ? actor.id.id : -1)
                + "|appliedNodes=" + appliedNodeCount);
        }

        private void DrawNodeList()
        {
            GUILayout.Space(6f);

            string nodeListLabel =
                (nodeListExpanded ? "▼ " : "▶ ")
                + "Authoring Bones";

            if (GUILayout.Button(nodeListLabel))
                nodeListExpanded = !nodeListExpanded;

            if (!nodeListExpanded)
                return;

            if (!EnsureAuthoringBoneSet())
            {
                GUILayout.Label(
                    authoringBonesStatus ??
                    "Authoring bone set is unavailable.",
                    GetWrappedLabelStyle());

                return;
            }

            bool requestedShowFingerJoints =
                GUILayout.Toggle(
                    showFingerJoints,
                    "Show finger joints (advanced)");

            if (requestedShowFingerJoints !=
                showFingerJoints)
            {
                showFingerJoints =
                    requestedShowFingerJoints;

                if (!showFingerJoints &&
                    authoringBones.IsFinger(
                        selectedPath))
                {
                    selectedPath = null;
                    symmetricEdit = false;
                }
            }

            GUILayout.Label(
                "Editable bones: "
                + authoringBones.BaseCount
                + " base"
                + " | "
                + authoringBones.FingerCount
                + " fingers "
                + (showFingerJoints
                    ? "shown"
                    : "hidden"));

            nodeScroll =
                GUILayout.BeginScrollView(
                    nodeScroll,
                    GUILayout.Height(GetNodeListHeight()));

            int matched = 0;
            int shown = 0;

            for (int i = 0; i < sourcePose.Nodes.Count; ++i)
            {
                PoseNodeSnapshot node = sourcePose.Nodes[i];

                if (!authoringBones.CanEdit(
                        node.Path,
                        showFingerJoints) ||
                    !MatchesFilter(
                        node.Path))
                {
                    continue;
                }

                ++matched;

                if (shown >= MaxVisibleMatches)
                    continue;

                ++shown;

                bool selected =
                    string.Equals(
                        selectedPath,
                        node.Path,
                        StringComparison.Ordinal);

                string label =
                    (selected ? "> " : "  ")
                    + (string.IsNullOrEmpty(node.Name)
                        ? node.Path
                        : node.Name);

                if (GUILayout.Button(label))
                    SelectNode(node);
            }

            GUILayout.EndScrollView();

            if (matched > MaxVisibleMatches)
            {
                GUILayout.Label(
                    "Matches: " + matched
                    + " | showing first " + MaxVisibleMatches
                    + " — narrow the filter");
            }
            else
            {
                GUILayout.Label("Matches: " + matched);
            }
        }

        private void DrawSelectedNode()
        {
            PoseNodeSnapshot node;
            if (!EnsureAuthoringBoneSet() ||
                string.IsNullOrEmpty(selectedPath) ||
                !authoringBones.CanEdit(
                    selectedPath,
                    showFingerJoints) ||
                !sourcePose.TryGetNode(
                    selectedPath,
                    out node))
            {
                GUILayout.Label("Selected node: none");
                return;
            }

            GUILayout.Label(
                "Selected node: "
                + (string.IsNullOrEmpty(node.Name)
                    ? node.Path
                    : node.Name));

            GUILayout.Label("Full hierarchy path:");
            GUILayout.Label(
                node.Path,
                GetWrappedLabelStyle());

            GUILayout.Label(
                "CRC32: 0x" + node.PathHash.ToString("X8"));

            GUILayout.Label(
                "Source local position: "
                + FormatVector3(node.LocalPosition));

            Vector3 currentLocalPosition;
            if (PoseOverrideRuntime.TryGetPosition(
                    actor,
                    node.Path,
                    out currentLocalPosition))
            {
                GUILayout.Label(
                    "Current local position: "
                    + FormatVector3(currentLocalPosition));
            }

            if (string.Equals(
                    node.Name,
                    "joint_pelvis_xyz",
                    StringComparison.Ordinal))
            {
                DrawPelvisHeightEditor(
                    node);
            }

            PoseNodeSnapshot counterpart;
            bool hasCounterpart =
                PoseCounterpartResolver.TryResolve(
                    sourcePose,
                    node.Path,
                    out counterpart) &&
                authoringBones.CanEdit(
                    counterpart.Path,
                    showFingerJoints);

            GUILayout.Space(4f);
            GUILayout.Label(
                "Counterpart: "
                + (hasCounterpart
                    ? counterpart.Name
                    : "none"));

            if (hasCounterpart)
            {
                GUILayout.Label(
                    counterpart.Path,
                    GetWrappedLabelStyle());
                GUILayout.Label(
                    "Mirror plane: joint_root local X = 0");
                GUILayout.Label(
                    "Selected-node rotation mirror is not a full branch pose mirror.");

                symmetricEdit =
                    GUILayout.Toggle(
                        symmetricEdit,
                        "Symmetric edit (geometric delta)");

                GUILayout.BeginHorizontal();

                if (GUILayout.Button("Mirror edit delta -> counterpart"))
                    MirrorSelectedEdit(node, counterpart, true);

                if (GUILayout.Button("Select counterpart"))
                    SelectNode(counterpart);

                GUILayout.EndHorizontal();

                if (GUILayout.Button("Mirror selected rotation -> counterpart"))
                    MirrorSelectedAbsoluteRotation(node, counterpart);

                GUILayout.Space(6f);

                if (PoseBranchMirror.IsPoseBranchRoot(
                        authoringBones,
                        node,
                        showFingerJoints) &&
                    PoseBranchMirror.IsPoseBranchRoot(
                        authoringBones,
                        counterpart,
                        showFingerJoints))
                {
                    GUILayout.Label(
                        "Authoring-bone branch mirror: position + rotation.");
                    GUILayout.Label(
                        "Non-primary helper and visual attachment nodes stay untouched.");
                    GUILayout.Label(
                        "Actual weapon root stays separate; Weapon follows hand can align it.");

                    GUILayout.BeginHorizontal();

                    if (GUILayout.Button("Mirror pose branch -> counterpart"))
                        MirrorSelectedBranch(node, counterpart);

                    if (GUILayout.Button("Reset counterpart branch edits"))
                        ResetCounterpartBranch(counterpart);

                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUILayout.Label(
                        "Branch mirror unavailable for this authoring bone.");
                }
            }
            else
            {
                symmetricEdit = false;
            }

            GUILayout.Space(6f);
            GUILayout.Label(
                "Local Rotation Euler"
                + " — internal override remains Quaternion");

            float x = DrawAngleSlider("X", selectedEuler.x);
            float y = DrawAngleSlider("Y", selectedEuler.y);
            float z = DrawAngleSlider("Z", selectedEuler.z);

            if (!Mathf.Approximately(x, selectedEuler.x) ||
                !Mathf.Approximately(y, selectedEuler.y) ||
                !Mathf.Approximately(z, selectedEuler.z))
            {
                selectedEuler = new Vector3(x, y, z);

                PoseOverrideRuntime.SetRotation(
                    actor,
                    node.Path,
                    node.LocalRotation,
                    Quaternion.Euler(selectedEuler));

                if (symmetricEdit && hasCounterpart)
                    MirrorSelectedEdit(node, counterpart, false);
            }

            if (GUILayout.Button("Reset selected"))
            {
                PoseOverrideRuntime.ResetNode(
                    actor,
                    node.Path);

                if (symmetricEdit && hasCounterpart)
                {
                    PoseOverrideRuntime.ResetNode(
                        actor,
                        counterpart.Path);
                }

                selectedEuler =
                    ToSignedEuler(node.LocalRotation.eulerAngles);
            }
        }

        private void DrawPelvisHeightEditor(
            PoseNodeSnapshot node)
        {
            Vector3 currentPosition =
                node.LocalPosition;

            PoseOverrideRuntime.TryGetPosition(
                actor,
                node.Path,
                out currentPosition);

            float currentOffset =
                currentPosition.y -
                node.LocalPosition.y;

            GUILayout.Space(6f);
            GUILayout.Label(
                "Pelvis Height — local Y only");

            GUILayout.BeginHorizontal();

            GUILayout.Label(
                "Offset "
                + currentOffset.ToString("F3"),
                GUILayout.Width(110f));

            float requestedOffset =
                GUILayout.HorizontalSlider(
                    currentOffset,
                    PelvisHeightMinOffset,
                    PelvisHeightMaxOffset);

            GUILayout.EndHorizontal();

            GUILayout.Label(
                "Range: "
                + PelvisHeightMinOffset.ToString("F1")
                + " .. +"
                + PelvisHeightMaxOffset.ToString("F1")
                + " | X/Z remain at Source Pose values");

            if (Mathf.Approximately(
                    requestedOffset,
                    currentOffset))
            {
                return;
            }

            Vector3 requestedPosition =
                node.LocalPosition;

            requestedPosition.y +=
                requestedOffset;

            // pelvis local Y만 authoring해 root의 수평 위치와 방향은 gameplay system에 맡김
            PoseOverrideRuntime.SetPosition(
                actor,
                node.Path,
                node.LocalPosition,
                requestedPosition);
        }

        private void MirrorSelectedEdit(
            PoseNodeSnapshot sourceNode,
            PoseNodeSnapshot targetNode,
            bool logResult)
        {
            float sourceDeltaDegrees;
            float targetDeltaDegrees;

            if (!PoseSymmetry.MirrorRotationEdit(
                    actor,
                    sourcePose,
                    sourceNode,
                    targetNode,
                    out sourceDeltaDegrees,
                    out targetDeltaDegrees))
            {
                if (logResult)
                {
                    AnimationLibraryLog.Warn(
                        "PoseLab mirror failed"
                        + " | source=" + sourceNode.Path
                        + " | target=" + targetNode.Path);
                }

                return;
            }

            if (!logResult)
                return;

            AnimationLibraryLog.Info(
                "PoseLab|MIRROR_DELTA_APPLIED"
                + "|source=" + sourceNode.Path
                + "|target=" + targetNode.Path
                + "|plane=joint_root_local_x"
                + "|sourceDeltaDeg=" + sourceDeltaDegrees.ToString("F3")
                + "|targetDeltaDeg=" + targetDeltaDegrees.ToString("F3"));
        }

        private void MirrorSelectedAbsoluteRotation(
            PoseNodeSnapshot sourceNode,
            PoseNodeSnapshot targetNode)
        {
            float targetChangeDegrees;
            float mirrorErrorDegrees;

            if (!PoseSymmetry.MirrorAbsoluteRotation(
                    actor,
                    sourcePose,
                    sourceNode,
                    targetNode,
                    out targetChangeDegrees,
                    out mirrorErrorDegrees))
            {
                AnimationLibraryLog.Warn(
                    "PoseLab absolute mirror failed"
                    + " | source=" + sourceNode.Path
                    + " | target=" + targetNode.Path);
                return;
            }

            AnimationLibraryLog.Info(
                "PoseLab|SELECTED_ROTATION_MIRROR_APPLIED"
                + "|source=" + sourceNode.Path
                + "|target=" + targetNode.Path
                + "|plane=joint_root_local_x"
                + "|targetChangeDeg=" + targetChangeDegrees.ToString("F3")
                + "|mirrorErrorDeg=" + mirrorErrorDegrees.ToString("F4"));
        }

        private void MirrorSelectedBranch(
            PoseNodeSnapshot sourceNode,
            PoseNodeSnapshot targetNode)
        {
            PoseBranchMirrorResult result;
            if (!PoseBranchMirror.Mirror(
                    actor,
                    sourcePose,
                    authoringBones,
                    showFingerJoints,
                    sourceNode,
                    targetNode,
                    out result))
            {
                AnimationLibraryLog.Warn(
                    "PoseLab branch mirror failed"
                    + " | source=" + sourceNode.Path
                    + " | target=" + targetNode.Path);
                return;
            }

            if (WeaponFollowRuntime.IsEnabledFor(actor))
                WeaponFollowRuntime.Apply(actor);

            AnimationLibraryLog.Info(
                "PoseLab|BRANCH_MIRROR_APPLIED"
                + "|sourceRoot=" + sourceNode.Path
                + "|targetRoot=" + targetNode.Path
                + "|plane=joint_root_local_x"
                + "|sourceNodes=" + result.SourceNodeCount
                + "|poseNodes=" + result.PoseNodeCount
                + "|pairedNodes=" + result.PairedNodeCount
                + "|skippedNodes=" + result.SkippedNodeCount
                + "|excludedNodes=" + result.ExcludedNodeCount
                + "|maxPositionError="
                + result.MaxPositionError.ToString("F6")
                + "|maxRotationErrorDeg="
                + result.MaxRotationErrorDegrees.ToString("F4"));
        }

        private void ResetCounterpartBranch(
            PoseNodeSnapshot targetNode)
        {
            int resetCount =
                PoseOverrideRuntime.ResetBranch(
                    actor,
                    targetNode.Path);

            if (WeaponFollowRuntime.IsEnabledFor(actor))
                WeaponFollowRuntime.Apply(actor);

            AnimationLibraryLog.Info(
                "PoseLab|BRANCH_EDITS_RESET"
                + "|targetRoot=" + targetNode.Path
                + "|resetNodes=" + resetCount);
        }

        private void SelectNode(PoseNodeSnapshot node)
        {
            if (node == null ||
                !EnsureAuthoringBoneSet() ||
                !authoringBones.CanEdit(
                    node.Path,
                    showFingerJoints))
            {
                return;
            }

            selectedPath = node.Path;

            Quaternion overrideRotation;
            if (PoseOverrideRuntime.TryGetRotation(
                    actor,
                    node.Path,
                    out overrideRotation))
            {
                selectedEuler =
                    ToSignedEuler(overrideRotation.eulerAngles);
            }
            else
            {
                selectedEuler =
                    ToSignedEuler(node.LocalRotation.eulerAngles);
            }
        }

        private void ResetAllEdits()
        {
            PoseOverrideRuntime.ClearAll(actor);
            PlanningPreviewRendererRefresh.Restore();

            if (WeaponFollowRuntime.IsEnabledFor(actor))
                WeaponFollowRuntime.Apply(actor);

            PoseNodeSnapshot node;
            if (!string.IsNullOrEmpty(selectedPath) &&
                sourcePose.TryGetNode(selectedPath, out node))
            {
                selectedEuler =
                    ToSignedEuler(node.LocalRotation.eulerAngles);
            }
        }

        private bool MatchesFilter(string path)
        {
            if (string.IsNullOrEmpty(filterText))
                return true;

            return path.IndexOf(
                       filterText,
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }


        private void SaveActiveActorState()
        {
            int actorId;
            if (!TryGetActorId(actor, out actorId))
                return;

            ActorWorkspaceState state;
            if (!actorStates.TryGetValue(actorId, out state) ||
                state.Actor != actor)
            {
                state =
                    new ActorWorkspaceState
                    {
                        Actor = actor
                    };

                actorStates[actorId] = state;
            }

            state.OriginalPose = originalPose;
            state.SourcePose = sourcePose;
            state.SourcePoseName = sourcePoseName;
            state.FilterText = filterText;
            state.SelectedPath = selectedPath;
            state.SelectedEuler = selectedEuler;
            state.SymmetricEdit = symmetricEdit;
            state.ShowFingerJoints = showFingerJoints;
            state.AuthoringBones = authoringBones;
            state.SourcePoseScroll = sourcePoseScroll;
            state.NodeScroll = nodeScroll;
            state.SequenceScroll = sequenceContentScroll;
            state.Sequence = sequence;
            state.SequenceTime = sequenceTime;
            state.SelectedSequenceKeyframe =
                selectedSequenceKeyframe;

            state.LastSequenceExportPath =
                lastSequenceExportPath;

            state.LastSequenceExportStatus =
                lastSequenceExportStatus;
        }

        private void LoadActiveActorState()
        {
            originalPose = null;
            sourcePose = null;
            sourcePoseName = "none";
            filterText = string.Empty;
            selectedPath = null;
            selectedEuler = Vector3.zero;
            symmetricEdit = false;
            showFingerJoints = false;
            authoringBones = null;
            authoringBonesStatus = null;
            sourcePoseScroll = Vector2.zero;
            nodeScroll = Vector2.zero;
            sequenceContentScroll = Vector2.zero;
            sequence = new PoseSequence();
            sequenceTime = 0f;
            selectedSequenceKeyframe = -1;
            lastSequenceSample = null;
            lastSequenceExportPath = null;
            lastSequenceExportStatus = null;

            int actorId;
            if (!TryGetActorId(actor, out actorId))
                return;

            ActorWorkspaceState state;
            if (!actorStates.TryGetValue(actorId, out state) ||
                state.Actor != actor)
            {
                actorStates[actorId] =
                    new ActorWorkspaceState
                    {
                        Actor = actor
                    };

                return;
            }

            originalPose = state.OriginalPose;
            sourcePose = state.SourcePose;
            sourcePoseName = state.SourcePoseName;
            filterText = state.FilterText;
            selectedPath = state.SelectedPath;
            selectedEuler = state.SelectedEuler;
            symmetricEdit = state.SymmetricEdit;
            showFingerJoints = state.ShowFingerJoints;
            authoringBones = state.AuthoringBones;
            sourcePoseScroll = state.SourcePoseScroll;
            nodeScroll = state.NodeScroll;
            sequenceContentScroll = state.SequenceScroll;
            sequence =
                state.Sequence ?? new PoseSequence();
            sequenceTime = state.SequenceTime;
            selectedSequenceKeyframe =
                state.SelectedSequenceKeyframe;
            lastSequenceSample = null;
            lastSequenceExportPath =
                state.LastSequenceExportPath;

            lastSequenceExportStatus =
                state.LastSequenceExportStatus;
        }

        private bool EnsureAuthoringBoneSet()
        {
            if (authoringBones != null &&
                authoringBones.IsBoundTo(
                    actor))
            {
                return true;
            }

            PoseAuthoringBoneSet resolved;
            string error;

            if (!PoseAuthoringBoneSet.TryCreate(
                    actor,
                    out resolved,
                    out error))
            {
                authoringBones = null;
                authoringBonesStatus =
                    "Authoring bones unavailable | "
                    + error;

                return false;
            }

            authoringBones =
                resolved;

            authoringBonesStatus =
                "Authoring bones ready"
                + " | base="
                + resolved.BaseCount
                + " | fingers="
                + resolved.FingerCount
                + " | total="
                + resolved.Count;

            AnimationLibraryLog.Info(
                "PoseLab|AUTHORING_BONES_READY"
                + "|actor="
                + (actor != null && actor.hasId
                    ? actor.id.id
                    : -1)
                + "|base="
                + resolved.BaseCount
                + "|fingers="
                + resolved.FingerCount
                + "|total="
                + resolved.Count);

            return true;
        }

        private static void ApplyActorPreview(CombatEntity target)
        {
            if (target == null)
                return;

            PoseSourceRuntime.Apply(target);
            PoseOverrideRuntime.Apply(target);
            AnimationClipValidationRuntime.ApplyFromRenderFallback(target);
            WeaponFollowRuntime.Apply(target);
        }

        private static bool TryGetActorId(
            CombatEntity target,
            out int actorId)
        {
            actorId = -1;

            if (target == null || !target.hasId)
                return false;

            actorId = target.id.id;
            return true;
        }

        private void ReleaseAllActorStates()
        {
            SaveActiveActorState();
            AnimationClipValidationRuntime.StopAll();

            foreach (ActorWorkspaceState state in actorStates.Values)
            {
                if (state == null || state.Actor == null)
                    continue;

                WeaponFollowRuntime.Disable(
                    state.Actor,
                    false);

                PoseOverrideRuntime.ClearAll(
                    state.Actor);

                PoseSourceRuntime.Clear(
                    state.Actor);

                if (state.OriginalPose == null)
                    continue;

                Transform samplingRoot;
                Transform jointRoot;
                if (!VisibleMechRigResolver.TryResolve(
                        state.Actor,
                        out samplingRoot,
                        out jointRoot))
                {
                    continue;
                }

                int appliedNodeCount;
                PoseSnapshotApply.TryApply(
                    state.Actor,
                    state.OriginalPose,
                    out appliedNodeCount);
            }

            actorStates.Clear();
            PoseSourceRuntime.ClearAll();
            PoseOverrideRuntime.ClearAllWithoutRestore();
            WeaponFollowRuntime.DisableAllWithoutRestore();
            PlanningPreviewRendererRefresh.Restore();
        }

        private static bool DrawWindowCollapseButton(
            ref bool collapsed,
            Rect windowRect)
        {
            Rect buttonRect =
                new Rect(
                    windowRect.width - 26f,
                    2f,
                    22f,
                    18f);

            if (GUI.Button(
                    buttonRect,
                    collapsed ? "▶" : "▼"))
            {
                collapsed = !collapsed;
            }

            if (!collapsed)
                return false;

            GUI.DragWindow(
                new Rect(
                    0f,
                    0f,
                    Mathf.Max(
                        0f,
                        windowRect.width - 30f),
                    WindowCollapsedHeight));

            return true;
        }

        private void ClampWindowsToScreen()
        {
            ClampWindowToScreen(
                ref sourceWindowRect,
                SourceWindowWidth,
                sourceWindowCollapsed
                    ? WindowCollapsedHeight
                    : WindowDefaultHeight);

            ClampWindowToScreen(
                ref editorWindowRect,
                EditorWindowWidth,
                editorWindowCollapsed
                    ? WindowCollapsedHeight
                    : WindowDefaultHeight);

            ClampWindowToScreen(
                ref sequenceWindowRect,
                SequenceWindowWidth,
                sequenceWindowCollapsed
                    ? WindowCollapsedHeight
                    : SequenceWindowHeight);
        }

        private static void ClampWindowToScreen(
            ref Rect windowRect,
            float preferredWidth)
        {
            ClampWindowToScreen(
                ref windowRect,
                preferredWidth,
                WindowDefaultHeight);
        }

        private static void ClampWindowToScreen(
            ref Rect windowRect,
            float preferredWidth,
            float preferredHeight)
        {
            float availableWidth =
                Mathf.Max(
                    240f,
                    Screen.width - ScreenMargin * 2f);

            float availableHeight =
                Mathf.Max(
                    WindowMinHeight,
                    Screen.height - ScreenMargin * 2f);

            windowRect.width =
                Mathf.Min(
                    preferredWidth,
                    availableWidth);

            windowRect.height =
                Mathf.Min(
                    preferredHeight,
                    availableHeight);

            float maxX =
                Mathf.Max(
                    ScreenMargin,
                    Screen.width
                    - windowRect.width
                    - ScreenMargin);

            float maxY =
                Mathf.Max(
                    ScreenMargin,
                    Screen.height
                    - windowRect.height
                    - ScreenMargin);

            windowRect.x =
                Mathf.Clamp(
                    windowRect.x,
                    ScreenMargin,
                    maxX);

            windowRect.y =
                Mathf.Clamp(
                    windowRect.y,
                    ScreenMargin,
                    maxY);
        }

        private float GetNodeListHeight()
        {
            return Mathf.Clamp(
                editorWindowRect.height * 0.40f,
                NodeListMinHeight,
                NodeListMaxHeight);
        }

        private static GUIStyle GetWrappedLabelStyle()
        {
            GUIStyle style =
                new GUIStyle(GUI.skin.label);

            style.wordWrap = true;
            return style;
        }

        private static float DrawAngleSlider(
            string axis,
            float value)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label(
                axis + " " + value.ToString("F1"),
                GUILayout.Width(72f));

            float result =
                GUILayout.HorizontalSlider(
                    value,
                    -180f,
                    180f);

            GUILayout.EndHorizontal();
            return result;
        }

        private static Vector3 ToSignedEuler(Vector3 euler)
        {
            return new Vector3(
                NormalizeAngle(euler.x),
                NormalizeAngle(euler.y),
                NormalizeAngle(euler.z));
        }

        private static float NormalizeAngle(float angle)
        {
            return Mathf.DeltaAngle(0f, angle);
        }

        private static string FormatVector3(Vector3 value)
        {
            return value.x.ToString("F4")
                + ", "
                + value.y.ToString("F4")
                + ", "
                + value.z.ToString("F4");
        }

        private void OnDestroy()
        {
            ReleaseAllActorStates();
            PoseLabWindowRuntime.Release(this);
        }
    }
}
