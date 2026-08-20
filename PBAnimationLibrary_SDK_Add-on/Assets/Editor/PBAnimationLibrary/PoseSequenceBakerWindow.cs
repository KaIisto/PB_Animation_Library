using System;
using System.Collections.Generic;
using System.IO;
using PB_AnimationLibrary.Exchange;
using UnityEditor;
using UnityEngine;

namespace PB_AnimationLibrary.SDKAuthoring
{
    internal sealed class PoseSequenceBakerWindow : EditorWindow
    {
        private const string DefaultOutputFolder =
            "Assets/User/PBAnimationLibrary/Generated";

        private const string DefaultBundleName =
            "pbalibanimations";

        private string inputPath = string.Empty;
        private string outputFolder = DefaultOutputFolder;
        private string bundleName = DefaultBundleName;
        private string clipAssetName = string.Empty;
        private bool overwriteExistingClip;

        private PoseSequenceBakeExchangeFile exchange;
        private string status = "Select a .pbalibpose.json export.";

        private AnimationClip previewClip;
        private GameObject previewRoot;
        private float previewTime;
        private bool previewPlaying;
        private bool previewLoop = true;
        private bool ownsAnimationMode;
        private double lastPreviewEditorTime;
        private string previewStatus =
            "Bake a clip, then choose a compatible preview root.";

        private string previewCompatibilityStatus =
            "Compatibility: preview clip/root not selected.";

        private MessageType previewCompatibilityMessageType =
            MessageType.Info;

        private bool showSkeletonPreview = true;
        private bool showSkeletonNames;

        private GameObject visualProxySource;
        private bool showVisualProxy = true;
        private string visualProxyStatus =
            "Select the SDK armor_set_skeleton-replace reference model.";

        private bool showAdvancedPreviewSetup;
        private string quickPreviewStatus =
            "Select an AnimationClip. Preview rig and SDK reference visuals are prepared automatically.";

        [MenuItem(
            "Tools/PB Animation Library/Pose Sequence Baker")]
        private static void Open()
        {
            PoseSequenceBakerWindow window =
                GetWindow<PoseSequenceBakerWindow>();

            ConfigureWindow(
                window);

            window.Show();
        }

        [MenuItem(
            "Tools/PB Animation Library/Animation Preview")]
        private static void OpenAnimationPreview()
        {
            PoseSequenceBakerWindow window =
                GetWindow<PoseSequenceBakerWindow>();

            ConfigureWindow(
                window);

            window.Show();

            AnimationClip selected =
                Selection.activeObject as AnimationClip;

            if (selected != null)
            {
                window.SetPreviewClip(
                    selected);

                window.PrepareAutomaticPreview();
            }
        }

        private static void ConfigureWindow(
            PoseSequenceBakerWindow window)
        {
            window.titleContent =
                new GUIContent(
                    "PB Pose Baker");

            window.minSize =
                new Vector2(
                    540f,
                    300f);
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            SceneView.duringSceneGui -= OnSceneGUI;
            StopPreviewAndRestore();
        }

        private void OnDestroy()
        {
            StopPreviewAndRestore();
        }

        private void OnGUI()
        {
            GUILayout.Label(
                "PB Animation Library Pose Sequence Baker",
                EditorStyles.boldLabel);

            GUILayout.Space(4f);

            DrawInputFile();

            GUILayout.Space(8f);

            DrawExchangeSummary();

            GUILayout.Space(8f);

            DrawBakeSettings();

            GUILayout.Space(8f);

            GUI.enabled =
                exchange != null;

            if (GUILayout.Button(
                    "Bake .anim"))
            {
                Bake();
            }

            GUI.enabled = true;

            GUILayout.Space(8f);

            EditorGUILayout.HelpBox(
                status,
                MessageType.Info);

            GUILayout.Space(12f);

            DrawPreview();
        }

        private void DrawBakeSettings()
        {
            GUILayout.Label(
                "Bake Settings",
                EditorStyles.boldLabel);

            clipAssetName =
                EditorGUILayout.TextField(
                    "Clip asset name",
                    clipAssetName);

            bundleName =
                EditorGUILayout.TextField(
                    "AssetBundle name",
                    bundleName);

            outputFolder =
                EditorGUILayout.TextField(
                    "Output asset folder",
                    outputFolder);

            overwriteExistingClip =
                EditorGUILayout.Toggle(
                    "Overwrite existing .anim",
                    overwriteExistingClip);

            EditorGUILayout.HelpBox(
                overwriteExistingClip
                    ? "An existing .anim with the same name will be replaced."
                    : "Existing .anim files are preserved. A numeric suffix is added when needed.",
                overwriteExistingClip
                    ? MessageType.Warning
                    : MessageType.Info);

            EditorGUILayout.LabelField(
                "Bundle assignment",
                string.IsNullOrEmpty(
                    SanitizeBundleName(
                        bundleName))
                    ? "(none)"
                    : SanitizeBundleName(
                        bundleName));
        }

        private void DrawInputFile()
        {
            GUILayout.Label(
                "Bake exchange JSON");

            GUILayout.BeginHorizontal();

            EditorGUILayout.SelectableLabel(
                string.IsNullOrEmpty(inputPath)
                    ? "(none)"
                    : inputPath,
                EditorStyles.textField,
                GUILayout.Height(
                    EditorGUIUtility.singleLineHeight));

            if (GUILayout.Button(
                    "Browse",
                    GUILayout.Width(80f)))
            {
                string startDirectory =
                    string.IsNullOrEmpty(inputPath)
                        ? Application.dataPath
                        : Path.GetDirectoryName(inputPath);

                string selected =
                    EditorUtility.OpenFilePanel(
                        "Select PB Animation Library bake exchange",
                        startDirectory,
                        "json");

                if (!string.IsNullOrEmpty(selected))
                    LoadExchange(selected);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawExchangeSummary()
        {
            if (exchange == null)
                return;

            EditorGUILayout.LabelField(
                "Clip",
                exchange.clipName);

            EditorGUILayout.LabelField(
                "Source pose",
                exchange.sourcePoseName);

            EditorGUILayout.LabelField(
                "Duration",
                exchange.duration.ToString("F3") + " s");

            EditorGUILayout.LabelField(
                "Frame rate",
                exchange.frameRate.ToString("F0") + " FPS");

            EditorGUILayout.LabelField(
                "Tracks",
                exchange.tracks != null
                    ? exchange.tracks.Length.ToString()
                    : "0");

            EditorGUILayout.LabelField(
                "Source keyframes",
                exchange.sourceKeyframeCount.ToString());
        }

        private void LoadExchange(string path)
        {
            try
            {
                string json =
                    File.ReadAllText(path);

                PoseSequenceBakeExchangeFile loaded =
                    JsonUtility.FromJson<PoseSequenceBakeExchangeFile>(
                        json);

                string validationError;
                if (!ValidateExchange(
                        loaded,
                        out validationError))
                {
                    exchange = null;
                    inputPath = path;
                    status =
                        "Invalid exchange: "
                        + validationError;
                    return;
                }

                exchange = loaded;
                inputPath = path;
                clipAssetName =
                    SanitizeAssetName(
                        loaded.clipName);

                status =
                    "Loaded "
                    + loaded.clipName
                    + " with "
                    + loaded.tracks.Length
                    + " tracks."
                    + " | SDK clip name="
                    + clipAssetName;
            }
            catch (Exception exception)
            {
                exchange = null;
                inputPath = path;
                status =
                    "Load failed: "
                    + exception.Message;
            }
        }

        private void Bake()
        {
            if (exchange == null)
                return;

            string validationError;
            if (!ValidateExchange(
                    exchange,
                    out validationError))
            {
                status =
                    "Bake blocked: "
                    + validationError;
                return;
            }

            string normalizedFolder =
                NormalizeAssetFolder(
                    outputFolder);

            if (string.IsNullOrEmpty(
                    normalizedFolder))
            {
                status =
                    "Output folder must be inside Assets/.";
                return;
            }

            EnsureAssetFolder(
                normalizedFolder);

            string requestedClipName =
                string.IsNullOrEmpty(
                    clipAssetName)
                    ? exchange.clipName
                    : clipAssetName;

            string safeClipName =
                SanitizeAssetName(
                    requestedClipName);

            string assetPath =
                normalizedFolder
                + "/"
                + safeClipName
                + ".anim";

            bool assetExists =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    assetPath) != null;

            bool overwritten =
                assetExists &&
                overwriteExistingClip;

            if (assetExists &&
                !overwriteExistingClip)
            {
                assetPath =
                    GetUniqueAnimationAssetPath(
                        normalizedFolder,
                        safeClipName);

                safeClipName =
                    Path.GetFileNameWithoutExtension(
                        assetPath);

                clipAssetName =
                    safeClipName;
            }

            AnimationClip clip =
                BuildClip(
                    exchange,
                    safeClipName);

            if (clip == null)
            {
                status =
                    "Bake failed while building AnimationClip curves.";
                return;
            }

            if (overwritten)
            {
                AssetDatabase.DeleteAsset(
                    assetPath);
            }

            AssetDatabase.CreateAsset(
                clip,
                assetPath);

            AssetDatabase.SaveAssets();

            string normalizedBundleName =
                SanitizeBundleName(
                    bundleName);

            if (!string.IsNullOrEmpty(
                    normalizedBundleName))
            {
                AssetImporter importer =
                    AssetImporter.GetAtPath(
                        assetPath);

                if (importer != null)
                {
                    importer.assetBundleName =
                        normalizedBundleName;

                    importer.SaveAndReimport();
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AnimationClip saved =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    assetPath);

            Selection.activeObject =
                saved;

            if (saved != null)
            {
                EditorGUIUtility.PingObject(saved);
                SetPreviewClip(saved);
            }

            status =
                "Baked: "
                + assetPath
                + " | clip="
                + safeClipName
                + " | bundle="
                + (string.IsNullOrEmpty(normalizedBundleName)
                    ? "(none)"
                    : normalizedBundleName)
                + " | overwritten="
                + overwritten
                + " | length="
                + clip.length.ToString("F3")
                + " | frameRate="
                + clip.frameRate.ToString("F0");
        }

        private void DrawPreview()
        {
            GUILayout.Label(
                "Animation Preview",
                EditorStyles.boldLabel);

            AnimationClip requestedClip =
                (AnimationClip)EditorGUILayout.ObjectField(
                    "AnimationClip",
                    previewClip,
                    typeof(AnimationClip),
                    false);

            if (requestedClip != previewClip)
            {
                SetPreviewClip(
                    requestedClip);

                if (requestedClip != null)
                {
                    PrepareAutomaticPreview();
                }
            }

            GUI.enabled =
                previewClip != null;

            if (GUILayout.Button(
                    "Prepare / Rebuild Preview"))
            {
                PrepareAutomaticPreview();
            }

            GUI.enabled = true;

            EditorGUILayout.HelpBox(
                quickPreviewStatus,
                GetQuickPreviewMessageType());

            DrawPlaybackControls();

            GUILayout.Space(8f);

            showAdvancedPreviewSetup =
                EditorGUILayout.Foldout(
                    showAdvancedPreviewSetup,
                    "Advanced / Manual Setup",
                    true);

            if (showAdvancedPreviewSetup)
            {
                DrawAdvancedPreviewSetup();
            }
        }

        private void DrawPlaybackControls()
        {
            int found = 0;
            int total = 0;

            bool compatible =
                previewClip != null &&
                previewRoot != null &&
                previewRoot.scene.IsValid() &&
                previewRoot.scene.isLoaded &&
                IsPreviewRootCompatible(
                    previewRoot,
                    previewClip,
                    out found,
                    out total);

            GUI.enabled =
                compatible;

            GUILayout.Space(4f);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button(
                    "|<",
                    GUILayout.Width(40f)))
            {
                SetPreviewTime(
                    0f);
            }

            if (GUILayout.Button(
                    "<",
                    GUILayout.Width(40f)))
            {
                StepPreview(
                    -1);
            }

            if (GUILayout.Button(
                    previewPlaying
                        ? "Pause"
                        : "Play",
                    GUILayout.Width(80f)))
            {
                if (previewPlaying)
                    PausePreview();
                else
                    PlayPreview();
            }

            if (GUILayout.Button(
                    ">",
                    GUILayout.Width(40f)))
            {
                StepPreview(
                    1);
            }

            if (GUILayout.Button(
                    ">|",
                    GUILayout.Width(40f)))
            {
                SetPreviewTime(
                    previewClip != null
                        ? previewClip.length
                        : 0f);
            }

            previewLoop =
                GUILayout.Toggle(
                    previewLoop,
                    "Loop");

            GUILayout.EndHorizontal();

            if (previewClip != null)
            {
                float duration =
                    Mathf.Max(
                        0f,
                        previewClip.length);

                EditorGUI.BeginChangeCheck();

                float requestedTime =
                    EditorGUILayout.Slider(
                        "Time",
                        previewTime,
                        0f,
                        duration);

                if (EditorGUI.EndChangeCheck())
                {
                    previewTime =
                        requestedTime;

                    PausePreview();
                    SamplePreview();
                }

                EditorGUILayout.LabelField(
                    "Frame",
                    GetPreviewFrameText());
            }

            GUILayout.BeginHorizontal();

            if (GUILayout.Button(
                    "Sample Current Time"))
            {
                SamplePreview();
            }

            if (GUILayout.Button(
                    "Reset Preview"))
            {
                StopPreviewAndRestore();
                previewTime = 0f;
                previewStatus =
                    "Preview reset and original scene transforms restored.";
            }

            GUILayout.EndHorizontal();

            GUI.enabled = true;
        }

        private void DrawAdvancedPreviewSetup()
        {
            GUILayout.Space(4f);

            GUILayout.Label(
                "Manual Preview Root",
                EditorStyles.boldLabel);

            GameObject requestedRoot =
                (GameObject)EditorGUILayout.ObjectField(
                    "Preview root",
                    previewRoot,
                    typeof(GameObject),
                    true);

            if (requestedRoot != previewRoot)
                SetPreviewRoot(requestedRoot);

            GUILayout.BeginHorizontal();

            GUI.enabled =
                previewClip != null;

            if (GUILayout.Button(
                    "Auto Find Compatible Root"))
            {
                AutoFindCompatibleRoot();
            }

            GUI.enabled =
                Selection.activeGameObject != null;

            if (GUILayout.Button(
                    "Use Selected GameObject"))
            {
                SetPreviewRoot(
                    Selection.activeGameObject);
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);

            GUILayout.Label(
                "Local Skeleton Preview",
                EditorStyles.boldLabel);

            GUI.enabled =
                Selection.activeGameObject != null;

            if (GUILayout.Button(
                    "Create Transform-Only Copy From Selected"))
            {
                CreateLocalSkeletonCopyFromSelected();
            }

            GUI.enabled = true;

            showSkeletonPreview =
                EditorGUILayout.Toggle(
                    "Show skeleton",
                    showSkeletonPreview);

            GUI.enabled =
                showSkeletonPreview;

            showSkeletonNames =
                EditorGUILayout.Toggle(
                    "Show joint names",
                    showSkeletonNames);

            GUI.enabled = true;

            DrawVisualProxyControls();

            DrawPreviewCompatibility();

            EditorGUILayout.HelpBox(
                previewStatus,
                GetPreviewMessageType());
        }

        private void PrepareAutomaticPreview()
        {
            if (previewClip == null)
            {
                quickPreviewStatus =
                    "Automatic preview FAILED | AnimationClip is missing.";

                return;
            }

            StopPreviewAndRestore();

            RemoveOwnedPreviewRoot();

            GameObject rigSource =
                FindProjectGameObject(
                    "unit_mech_body");

            if (rigSource == null)
            {
                quickPreviewStatus =
                    "Automatic preview FAILED | local unit_mech_body asset was not found. "
                    + "Keep your local extracted rig somewhere under Assets/.";

                previewRoot = null;
                UpdatePreviewCompatibilityStatus();
                return;
            }

            GameObject created =
                SkeletonPreviewUtility.CreateTransformOnlyCopy(
                    rigSource);

            if (created == null)
            {
                quickPreviewStatus =
                    "Automatic preview FAILED | transform-only preview rig could not be created.";

                previewRoot = null;
                UpdatePreviewCompatibilityStatus();
                return;
            }

            previewRoot =
                created;

            previewTime = 0f;
            previewPlaying = false;

            int found;
            int total;

            bool compatible =
                IsPreviewRootCompatible(
                    previewRoot,
                    previewClip,
                    out found,
                    out total);

            if (!compatible)
            {
                showSkeletonPreview = true;

                quickPreviewStatus =
                    "Automatic preview FAILED | clip bindings="
                    + found
                    + "/"
                    + total
                    + " | source="
                    + AssetDatabase.GetAssetPath(
                        rigSource);

                UpdatePreviewCompatibilityStatus();
                FramePreviewRoot();
                return;
            }

            visualProxySource =
                FindProjectGameObject(
                    "armor_set_skeleton-replace");

            bool proxyBuilt = false;
            int visualCount = 0;
            string proxyResult =
                "SDK reference model not found";

            if (visualProxySource != null)
            {
                proxyBuilt =
                    SkeletonVisualProxyUtility.TryBuildReplaceProxy(
                        previewRoot,
                        visualProxySource,
                        out visualCount,
                        out proxyResult);
            }

            showVisualProxy =
                proxyBuilt;

            showSkeletonPreview =
                !proxyBuilt;

            if (proxyBuilt)
            {
                SkeletonVisualProxyUtility.SetVisible(
                    previewRoot,
                    true);
            }

            SamplePreview();
            FramePreviewRoot();

            quickPreviewStatus =
                "Automatic preview READY"
                + " | paths="
                + found
                + "/"
                + total
                + " | proxy="
                + (proxyBuilt
                    ? visualCount.ToString()
                    : "skeleton-only")
                + " | rig="
                + AssetDatabase.GetAssetPath(
                    rigSource)
                + (visualProxySource != null
                    ? " | reference="
                      + AssetDatabase.GetAssetPath(
                          visualProxySource)
                    : " | reference=(not found)");

            visualProxyStatus =
                proxyBuilt
                    ? "Visual Proxy BUILT | "
                      + proxyResult
                    : "Visual Proxy unavailable | "
                      + proxyResult;

            UpdatePreviewCompatibilityStatus();
        }

        private void RemoveOwnedPreviewRoot()
        {
            if (previewRoot == null ||
                !previewRoot.scene.IsValid() ||
                !previewRoot.scene.isLoaded ||
                !previewRoot.name.StartsWith(
                    "[LOCAL] ",
                    StringComparison.Ordinal))
            {
                return;
            }

            SkeletonVisualProxyUtility.Remove(
                previewRoot);

            Undo.DestroyObjectImmediate(
                previewRoot);

            previewRoot = null;
        }

        private static GameObject FindProjectGameObject(
            string exactName)
        {
            string[] guids =
                AssetDatabase.FindAssets(
                    exactName
                    + " t:GameObject");

            GameObject best = null;
            int bestScore =
                int.MinValue;

            for (int i = 0;
                 i < guids.Length;
                 ++i)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guids[i]);

                GameObject candidate =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        path);

                if (candidate == null ||
                    !string.Equals(
                        candidate.name,
                        exactName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int score =
                    GetAutomaticAssetScore(
                        path);

                if (score <= bestScore)
                    continue;

                best =
                    candidate;

                bestScore =
                    score;
            }

            return best;
        }

        private static int GetAutomaticAssetScore(
            string path)
        {
            if (string.IsNullOrEmpty(
                    path))
            {
                return 0;
            }

            int score = 0;

            if (path.IndexOf(
                    "LocalResearch",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                score += 100;
            }

            if (path.IndexOf(
                    "VanillaExtract",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                score += 80;
            }

            if (path.IndexOf(
                    "PBAnimationLibrary",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                score += 40;
            }

            if (path.IndexOf(
                    "mech-and-weapon-sample-files",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                score += 30;
            }

            if (path.StartsWith(
                    "Assets/User/",
                    StringComparison.OrdinalIgnoreCase))
            {
                score += 10;
            }

            return score;
        }

        private void FramePreviewRoot()
        {
            if (previewRoot == null)
                return;

            Selection.activeGameObject =
                previewRoot;

            SceneView sceneView =
                SceneView.lastActiveSceneView;

            if (sceneView != null)
            {
                sceneView.FrameSelected();
                sceneView.Repaint();
            }
        }

        private MessageType GetQuickPreviewMessageType()
        {
            return quickPreviewStatus.StartsWith(
                       "Automatic preview FAILED",
                       StringComparison.Ordinal)
                ? MessageType.Warning
                : MessageType.Info;
        }

        private void DrawVisualProxyControls()
        {
            GUILayout.Space(6f);

            GUILayout.Label(
                "SDK Reference Visual Proxy",
                EditorStyles.boldLabel);

            GameObject requestedSource =
                (GameObject)EditorGUILayout.ObjectField(
                    "Reference model",
                    visualProxySource,
                    typeof(GameObject),
                    false);

            if (requestedSource != visualProxySource)
            {
                visualProxySource =
                    requestedSource;

                visualProxyStatus =
                    visualProxySource != null
                        ? "Reference model selected: "
                          + visualProxySource.name
                        : "Reference model cleared.";
            }

            GUILayout.BeginHorizontal();

            GameObject selected =
                Selection.activeObject as GameObject;

            GUI.enabled =
                selected != null &&
                AssetDatabase.Contains(
                    selected);

            if (GUILayout.Button(
                    "Use Selected Model"))
            {
                visualProxySource =
                    selected;

                visualProxyStatus =
                    "Reference model selected: "
                    + visualProxySource.name;
            }

            GUI.enabled =
                previewRoot != null &&
                previewRoot.scene.IsValid() &&
                previewRoot.scene.isLoaded &&
                visualProxySource != null;

            if (GUILayout.Button(
                    "Build / Rebuild SDK Replace Proxy"))
            {
                BuildVisualProxy();
            }

            GUILayout.EndHorizontal();

            bool requestedVisibility =
                EditorGUILayout.Toggle(
                    "Show proxy",
                    showVisualProxy);

            if (requestedVisibility !=
                showVisualProxy)
            {
                showVisualProxy =
                    requestedVisibility;

                SkeletonVisualProxyUtility.SetVisible(
                    previewRoot,
                    showVisualProxy);

                SceneView.RepaintAll();
            }

            GUI.enabled =
                previewRoot != null &&
                SkeletonVisualProxyUtility.HasProxy(
                    previewRoot);

            if (GUILayout.Button(
                    "Remove Visual Proxy"))
            {
                RemoveVisualProxy();
            }

            GUI.enabled = true;

            EditorGUILayout.HelpBox(
                visualProxyStatus,
                GetVisualProxyMessageType());
        }

        private void BuildVisualProxy()
        {
            if (previewRoot == null)
            {
                visualProxyStatus =
                    "Visual Proxy FAILED | preview root is missing.";

                return;
            }

            if (visualProxySource == null)
            {
                visualProxyStatus =
                    "Visual Proxy FAILED | reference model is missing.";

                return;
            }

            StopPreviewAndRestore();

            int visualCount;
            string result;

            bool built =
                SkeletonVisualProxyUtility.TryBuildReplaceProxy(
                    previewRoot,
                    visualProxySource,
                    out visualCount,
                    out result);

            showVisualProxy = true;

            visualProxyStatus =
                built
                    ? "Visual Proxy BUILT | "
                      + result
                    : "Visual Proxy FAILED | "
                      + result;

            if (built &&
                previewClip != null)
            {
                SamplePreview();
            }

            SceneView.RepaintAll();
        }

        private void RemoveVisualProxy()
        {
            StopPreviewAndRestore();

            int removed =
                SkeletonVisualProxyUtility.Remove(
                    previewRoot);

            visualProxyStatus =
                "Visual Proxy REMOVED | visuals="
                + removed;

            SceneView.RepaintAll();
        }

        private MessageType GetVisualProxyMessageType()
        {
            return visualProxyStatus.StartsWith(
                       "Visual Proxy FAILED",
                       StringComparison.Ordinal)
                ? MessageType.Warning
                : MessageType.Info;
        }

        private void CreateLocalSkeletonCopyFromSelected()
        {
            GameObject selected =
                Selection.activeGameObject;

            if (selected == null)
            {
                previewStatus =
                    "Local Skeleton FAILED | no source GameObject selected.";

                return;
            }

            if (previewRoot != null)
            {
                SkeletonVisualProxyUtility.Remove(
                    previewRoot);
            }

            GameObject copy =
                SkeletonPreviewUtility.CreateTransformOnlyCopy(
                    selected);

            if (copy == null)
            {
                previewStatus =
                    "Local Skeleton FAILED | transform-only copy could not be created.";

                return;
            }

            previewRoot =
                copy;

            previewTime = 0f;
            showSkeletonPreview = true;

            int found = 0;
            int total = 0;

            bool compatible =
                previewClip != null &&
                IsPreviewRootCompatible(
                    previewRoot,
                    previewClip,
                    out found,
                    out total);

            if (previewClip == null)
            {
                previewStatus =
                    "Local Skeleton CREATED"
                    + " | root="
                    + GetHierarchyPath(
                        previewRoot.transform)
                    + " | select a Preview clip to validate bindings.";
            }
            else
            {
                previewStatus =
                    "Local Skeleton CREATED"
                    + " | compatible="
                    + compatible
                    + " | paths="
                    + found
                    + "/"
                    + total
                    + " | root="
                    + GetHierarchyPath(
                        previewRoot.transform);
            }

            UpdatePreviewCompatibilityStatus();
            SceneView.RepaintAll();
        }

        private void OnSceneGUI(
            SceneView sceneView)
        {
            if (!showSkeletonPreview ||
                previewRoot == null)
            {
                return;
            }

            SkeletonPreviewUtility.Draw(
                previewRoot,
                showSkeletonNames);
        }

        private void DrawPreviewCompatibility()
        {
            UpdatePreviewCompatibilityStatus();

            EditorGUILayout.HelpBox(
                previewCompatibilityStatus,
                previewCompatibilityMessageType);
        }

        private void UpdatePreviewCompatibilityStatus()
        {
            if (previewClip == null)
            {
                previewCompatibilityStatus =
                    "Compatibility: choose or bake an AnimationClip.";

                previewCompatibilityMessageType =
                    MessageType.Info;

                return;
            }

            if (previewRoot == null)
            {
                previewCompatibilityStatus =
                    "Compatibility: no Preview Root selected.";

                previewCompatibilityMessageType =
                    MessageType.Info;

                return;
            }

            if (!previewRoot.scene.IsValid() ||
                !previewRoot.scene.isLoaded)
            {
                previewCompatibilityStatus =
                    "Compatibility: Preview Root is a prefab asset, not a loaded Scene object. "
                    + "Use Create Transform-Only Copy From Selected.";

                previewCompatibilityMessageType =
                    MessageType.Warning;

                return;
            }

            int found;
            int total;

            bool compatible =
                IsPreviewRootCompatible(
                    previewRoot,
                    previewClip,
                    out found,
                    out total);

            if (compatible)
            {
                previewCompatibilityStatus =
                    "Compatible preview root | paths="
                    + found
                    + "/"
                    + total;

                previewCompatibilityMessageType =
                    MessageType.Info;

                return;
            }

            previewCompatibilityStatus =
                "Incompatible preview root | paths="
                + found
                + "/"
                + total
                + " | root must contain every animated Transform binding path.";

            previewCompatibilityMessageType =
                MessageType.Warning;
        }

        private void SetPreviewClip(
            AnimationClip value)
        {
            if (previewClip == value)
                return;

            StopPreviewAndRestore();

            previewClip = value;
            previewTime = 0f;

            if (previewClip == null)
            {
                previewStatus =
                    "No preview clip selected.";

                quickPreviewStatus =
                    "Select an AnimationClip. Preview rig and SDK reference visuals are prepared automatically.";

                return;
            }

            previewStatus =
                "Preview clip selected: "
                + previewClip.name;

            quickPreviewStatus =
                "AnimationClip selected: "
                + previewClip.name
                + " | preparing automatically...";

            UpdatePreviewCompatibilityStatus();
        }

        private void SetPreviewRoot(
            GameObject value)
        {
            if (previewRoot == value)
                return;

            StopPreviewAndRestore();

            if (previewRoot != null)
            {
                SkeletonVisualProxyUtility.Remove(
                    previewRoot);
            }

            previewRoot = value;
            previewTime = 0f;

            previewStatus =
                previewRoot != null
                    ? "Preview root selected: "
                      + GetHierarchyPath(
                          previewRoot.transform)
                    : "Preview root cleared.";

            UpdatePreviewCompatibilityStatus();
        }

        private void AutoFindCompatibleRoot()
        {
            if (previewClip == null)
            {
                previewStatus =
                    "Auto Find FAILED | preview clip is missing.";

                return;
            }

            StopPreviewAndRestore();

            Transform[] transforms =
                Resources.FindObjectsOfTypeAll<Transform>();

            GameObject exactMatch = null;
            bool exactMatchActive = false;

            GameObject bestPartial = null;
            int bestFound = -1;
            int bestTotal = 0;

            int loadedSceneTransforms = 0;
            int jointRootCandidates = 0;

            for (int i = 0;
                 i < transforms.Length;
                 ++i)
            {
                Transform candidate =
                    transforms[i];

                if (candidate == null ||
                    candidate.gameObject == null ||
                    !candidate.gameObject.scene.IsValid() ||
                    !candidate.gameObject.scene.isLoaded)
                {
                    continue;
                }

                ++loadedSceneTransforms;

                if (candidate.Find("joint_root") == null)
                    continue;

                ++jointRootCandidates;

                int found;
                int total;

                bool compatible =
                    IsPreviewRootCompatible(
                        candidate.gameObject,
                        previewClip,
                        out found,
                        out total);

                if (found > bestFound)
                {
                    bestFound = found;
                    bestTotal = total;
                    bestPartial = candidate.gameObject;
                }

                if (!compatible)
                    continue;

                bool active =
                    candidate.gameObject.activeInHierarchy;

                if (exactMatch == null ||
                    (active && !exactMatchActive))
                {
                    exactMatch =
                        candidate.gameObject;

                    exactMatchActive =
                        active;

                    if (active)
                        break;
                }
            }

            if (exactMatch != null)
            {
                previewRoot = exactMatch;
                previewTime = 0f;

                int found;
                int total;

                IsPreviewRootCompatible(
                    previewRoot,
                    previewClip,
                    out found,
                    out total);

                previewStatus =
                    "Auto Find SUCCESS"
                    + " | paths="
                    + found
                    + "/"
                    + total
                    + " | root="
                    + GetHierarchyPath(
                        previewRoot.transform);

                UpdatePreviewCompatibilityStatus();
                return;
            }

            previewRoot = null;
            previewTime = 0f;

            if (jointRootCandidates == 0)
            {
                previewStatus =
                    "Auto Find FAILED"
                    + " | loadedSceneTransforms="
                    + loadedSceneTransforms
                    + " | jointRootCandidates=0"
                    + " | no loaded Scene object has a direct joint_root child.";

                UpdatePreviewCompatibilityStatus();
                return;
            }

            string bestPath =
                bestPartial != null
                    ? GetHierarchyPath(
                        bestPartial.transform)
                    : "(none)";

            previewStatus =
                "Auto Find FAILED"
                + " | loadedSceneTransforms="
                + loadedSceneTransforms
                + " | jointRootCandidates="
                + jointRootCandidates
                + " | bestPaths="
                + Mathf.Max(
                    0,
                    bestFound)
                + "/"
                + bestTotal
                + " | best="
                + bestPath;

            UpdatePreviewCompatibilityStatus();
        }

        private static bool IsPreviewRootCompatible(
            GameObject root,
            AnimationClip clip,
            out int found,
            out int total)
        {
            found = 0;
            total = 0;

            if (root == null ||
                clip == null)
            {
                return false;
            }

            EditorCurveBinding[] bindings =
                AnimationUtility.GetCurveBindings(
                    clip);

            HashSet<string> paths =
                new HashSet<string>(
                    StringComparer.Ordinal);

            for (int i = 0;
                 i < bindings.Length;
                 ++i)
            {
                EditorCurveBinding binding =
                    bindings[i];

                if (binding.type !=
                    typeof(Transform))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(
                        binding.path))
                {
                    continue;
                }

                paths.Add(
                    binding.path);
            }

            total =
                paths.Count;

            if (total == 0)
                return false;

            foreach (string path in paths)
            {
                if (root.transform.Find(path) != null)
                    ++found;
            }

            return found == total;
        }

        private void PlayPreview()
        {
            if (!CanPreview())
                return;

            if (!EnsureAnimationMode())
                return;

            previewPlaying = true;
            lastPreviewEditorTime =
                EditorApplication.timeSinceStartup;

            SamplePreview();
        }

        private void PausePreview()
        {
            previewPlaying = false;
        }

        private void StepPreview(int direction)
        {
            if (previewClip == null)
                return;

            float frameRate =
                Mathf.Max(
                    1f,
                    previewClip.frameRate);

            float delta =
                1f / frameRate;

            SetPreviewTime(
                previewTime +
                delta * direction);
        }

        private void SetPreviewTime(float value)
        {
            if (previewClip == null)
            {
                previewTime = 0f;
                return;
            }

            PausePreview();

            previewTime =
                Mathf.Clamp(
                    value,
                    0f,
                    Mathf.Max(
                        0f,
                        previewClip.length));

            SamplePreview();
        }

        private bool SamplePreview()
        {
            if (!CanPreview())
                return false;

            if (!EnsureAnimationMode())
                return false;

            AnimationMode.BeginSampling();

            try
            {
                AnimationMode.SampleAnimationClip(
                    previewRoot,
                    previewClip,
                    previewTime);
            }
            finally
            {
                AnimationMode.EndSampling();
            }

            SceneView.RepaintAll();
            Repaint();

            previewStatus =
                "Preview sampled | time="
                + previewTime.ToString("F3")
                + " / "
                + previewClip.length.ToString("F3");

            return true;
        }

        private bool CanPreview()
        {
            if (previewClip == null)
            {
                previewStatus =
                    "Preview clip is missing.";
                return false;
            }

            if (previewRoot == null)
            {
                previewStatus =
                    "Preview root is missing.";
                return false;
            }

            if (!previewRoot.scene.IsValid() ||
                !previewRoot.scene.isLoaded)
            {
                previewStatus =
                    "Preview root must be a Scene GameObject. "
                    + "Create a transform-only local copy or instantiate the source prefab first.";

                return false;
            }

            int found;
            int total;

            if (!IsPreviewRootCompatible(
                    previewRoot,
                    previewClip,
                    out found,
                    out total))
            {
                previewStatus =
                    "Preview root incompatible | paths="
                    + found
                    + "/"
                    + total;

                return false;
            }

            return true;
        }

        private bool EnsureAnimationMode()
        {
            if (ownsAnimationMode)
                return true;

            if (AnimationMode.InAnimationMode())
            {
                previewStatus =
                    "Another Unity AnimationMode session is active. Stop that preview first.";
                return false;
            }

            AnimationMode.StartAnimationMode();
            ownsAnimationMode = true;
            return true;
        }

        private void StopPreviewAndRestore()
        {
            previewPlaying = false;

            if (!ownsAnimationMode)
                return;

            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();

            ownsAnimationMode = false;
            SceneView.RepaintAll();
            Repaint();
        }

        private void OnEditorUpdate()
        {
            if (!previewPlaying ||
                previewClip == null ||
                previewRoot == null)
            {
                return;
            }

            double now =
                EditorApplication.timeSinceStartup;

            double delta =
                now -
                lastPreviewEditorTime;

            lastPreviewEditorTime =
                now;

            if (delta < 0d)
                delta = 0d;

            float duration =
                Mathf.Max(
                    0f,
                    previewClip.length);

            if (duration <= 0f)
            {
                previewTime = 0f;
                PausePreview();
                return;
            }

            previewTime +=
                (float)delta;

            if (previewTime > duration)
            {
                if (previewLoop)
                {
                    previewTime =
                        Mathf.Repeat(
                            previewTime,
                            duration);
                }
                else
                {
                    previewTime = duration;
                    PausePreview();
                }
            }

            SamplePreview();
        }

        private string GetPreviewFrameText()
        {
            if (previewClip == null)
                return "-";

            float frameRate =
                Mathf.Max(
                    1f,
                    previewClip.frameRate);

            int frame =
                Mathf.RoundToInt(
                    previewTime *
                    frameRate);

            int frameLimit =
                Mathf.RoundToInt(
                    previewClip.length *
                    frameRate);

            return frame
                + " / "
                + frameLimit
                + " @ "
                + frameRate.ToString("F0")
                + " FPS";
        }

        private MessageType GetPreviewMessageType()
        {
            if (previewStatus.StartsWith(
                    "Auto Find FAILED",
                    StringComparison.Ordinal) ||
                previewStatus.StartsWith(
                    "Preview root incompatible",
                    StringComparison.Ordinal) ||
                previewStatus.StartsWith(
                    "Preview root must be",
                    StringComparison.Ordinal) ||
                previewStatus.StartsWith(
                    "Local Skeleton FAILED",
                    StringComparison.Ordinal) ||
                previewStatus.StartsWith(
                    "Another Unity",
                    StringComparison.Ordinal))
            {
                return MessageType.Warning;
            }

            return MessageType.Info;
        }

        private static string GetHierarchyPath(
            Transform transform)
        {
            if (transform == null)
                return "(null)";

            string path =
                transform.name;

            Transform parent =
                transform.parent;

            while (parent != null)
            {
                path =
                    parent.name
                    + "/"
                    + path;

                parent =
                    parent.parent;
            }

            return path;
        }

        private static AnimationClip BuildClip(
            PoseSequenceBakeExchangeFile source,
            string clipName)
        {
            List<float> sampleTimes =
                BuildSampleTimes(source);

            if (sampleTimes.Count == 0)
                return null;

            AnimationClip clip =
                new AnimationClip
                {
                    name = clipName,
                    frameRate =
                        Mathf.Max(
                            1f,
                            source.frameRate),
                    legacy = false
                };

            for (int i = 0;
                 i < source.tracks.Length;
                 ++i)
            {
                PoseSequenceBakeTrack track =
                    source.tracks[i];

                if (track == null ||
                    string.IsNullOrEmpty(track.path))
                {
                    continue;
                }

                if (track.hasPosition)
                {
                    BakePositionTrack(
                        clip,
                        track,
                        sampleTimes);
                }

                if (track.hasRotation)
                {
                    BakeRotationTrack(
                        clip,
                        track,
                        sampleTimes);
                }
            }

            clip.EnsureQuaternionContinuity();
            return clip;
        }

        private static void BakePositionTrack(
            AnimationClip clip,
            PoseSequenceBakeTrack track,
            List<float> sampleTimes)
        {
            List<ScalarSample> x =
                new List<ScalarSample>();

            List<ScalarSample> y =
                new List<ScalarSample>();

            List<ScalarSample> z =
                new List<ScalarSample>();

            for (int i = 0;
                 i < sampleTimes.Count;
                 ++i)
            {
                float time =
                    sampleTimes[i];

                Vector3 value =
                    EvaluatePosition(
                        track.positionKeys,
                        time);

                x.Add(
                    new ScalarSample(
                        time,
                        value.x));

                y.Add(
                    new ScalarSample(
                        time,
                        value.y));

                z.Add(
                    new ScalarSample(
                        time,
                        value.z));
            }

            clip.SetCurve(
                track.path,
                typeof(Transform),
                "localPosition.x",
                CreateLinearCurve(x));

            clip.SetCurve(
                track.path,
                typeof(Transform),
                "localPosition.y",
                CreateLinearCurve(y));

            clip.SetCurve(
                track.path,
                typeof(Transform),
                "localPosition.z",
                CreateLinearCurve(z));
        }

        private static void BakeRotationTrack(
            AnimationClip clip,
            PoseSequenceBakeTrack track,
            List<float> sampleTimes)
        {
            List<ScalarSample> x =
                new List<ScalarSample>();

            List<ScalarSample> y =
                new List<ScalarSample>();

            List<ScalarSample> z =
                new List<ScalarSample>();

            List<ScalarSample> w =
                new List<ScalarSample>();

            Quaternion previous =
                Quaternion.identity;

            bool hasPrevious = false;

            for (int i = 0;
                 i < sampleTimes.Count;
                 ++i)
            {
                float time =
                    sampleTimes[i];

                Quaternion value =
                    EvaluateRotation(
                        track.rotationKeys,
                        time);

                if (hasPrevious &&
                    Quaternion.Dot(
                        previous,
                        value) < 0f)
                {
                    value =
                        new Quaternion(
                            -value.x,
                            -value.y,
                            -value.z,
                            -value.w);
                }

                previous = value;
                hasPrevious = true;

                x.Add(
                    new ScalarSample(
                        time,
                        value.x));

                y.Add(
                    new ScalarSample(
                        time,
                        value.y));

                z.Add(
                    new ScalarSample(
                        time,
                        value.z));

                w.Add(
                    new ScalarSample(
                        time,
                        value.w));
            }

            clip.SetCurve(
                track.path,
                typeof(Transform),
                "localRotation.x",
                CreateLinearCurve(x));

            clip.SetCurve(
                track.path,
                typeof(Transform),
                "localRotation.y",
                CreateLinearCurve(y));

            clip.SetCurve(
                track.path,
                typeof(Transform),
                "localRotation.z",
                CreateLinearCurve(z));

            clip.SetCurve(
                track.path,
                typeof(Transform),
                "localRotation.w",
                CreateLinearCurve(w));
        }

        private static List<float> BuildSampleTimes(
            PoseSequenceBakeExchangeFile source)
        {
            List<float> times =
                new List<float>();

            AddUniqueTime(
                times,
                0f);

            float frameRate =
                Mathf.Max(
                    1f,
                    source.frameRate);

            float duration =
                Mathf.Max(
                    0.01f,
                    source.duration);

            int frameCount =
                Mathf.CeilToInt(
                    duration * frameRate);

            for (int frame = 0;
                 frame <= frameCount;
                 ++frame)
            {
                float time =
                    Mathf.Min(
                        duration,
                        frame / frameRate);

                AddUniqueTime(
                    times,
                    time);
            }

            for (int trackIndex = 0;
                 trackIndex < source.tracks.Length;
                 ++trackIndex)
            {
                PoseSequenceBakeTrack track =
                    source.tracks[trackIndex];

                if (track == null)
                    continue;

                if (track.positionKeys != null)
                {
                    for (int i = 0;
                         i < track.positionKeys.Length;
                         ++i)
                    {
                        AddUniqueTime(
                            times,
                            Mathf.Clamp(
                                track.positionKeys[i].time,
                                0f,
                                duration));
                    }
                }

                if (track.rotationKeys != null)
                {
                    for (int i = 0;
                         i < track.rotationKeys.Length;
                         ++i)
                    {
                        AddUniqueTime(
                            times,
                            Mathf.Clamp(
                                track.rotationKeys[i].time,
                                0f,
                                duration));
                    }
                }
            }

            AddUniqueTime(
                times,
                duration);

            times.Sort();
            return times;
        }

        private static void AddUniqueTime(
            List<float> times,
            float value)
        {
            const float tolerance = 0.00001f;

            for (int i = 0;
                 i < times.Count;
                 ++i)
            {
                if (Mathf.Abs(
                        times[i] - value) <=
                    tolerance)
                {
                    return;
                }
            }

            times.Add(value);
        }

        private static Vector3 EvaluatePosition(
            PoseSequenceBakePositionKey[] keys,
            float time)
        {
            if (keys == null ||
                keys.Length == 0)
            {
                return Vector3.zero;
            }

            if (keys.Length == 1 ||
                time <= keys[0].time)
            {
                return ToVector3(
                    keys[0]);
            }

            int lastIndex =
                keys.Length - 1;

            if (time >=
                keys[lastIndex].time)
            {
                return ToVector3(
                    keys[lastIndex]);
            }

            for (int i = 1;
                 i < keys.Length;
                 ++i)
            {
                PoseSequenceBakePositionKey next =
                    keys[i];

                if (next.time < time)
                    continue;

                PoseSequenceBakePositionKey previous =
                    keys[i - 1];

                float duration =
                    next.time -
                    previous.time;

                float factor =
                    duration > 0.000001f
                        ? Mathf.Clamp01(
                            (time - previous.time) /
                            duration)
                        : 0f;

                return Vector3.Lerp(
                    ToVector3(previous),
                    ToVector3(next),
                    factor);
            }

            return ToVector3(
                keys[lastIndex]);
        }

        private static Quaternion EvaluateRotation(
            PoseSequenceBakeRotationKey[] keys,
            float time)
        {
            if (keys == null ||
                keys.Length == 0)
            {
                return Quaternion.identity;
            }

            if (keys.Length == 1 ||
                time <= keys[0].time)
            {
                return ToQuaternion(
                    keys[0]);
            }

            int lastIndex =
                keys.Length - 1;

            if (time >=
                keys[lastIndex].time)
            {
                return ToQuaternion(
                    keys[lastIndex]);
            }

            for (int i = 1;
                 i < keys.Length;
                 ++i)
            {
                PoseSequenceBakeRotationKey next =
                    keys[i];

                if (next.time < time)
                    continue;

                PoseSequenceBakeRotationKey previous =
                    keys[i - 1];

                float duration =
                    next.time -
                    previous.time;

                float factor =
                    duration > 0.000001f
                        ? Mathf.Clamp01(
                            (time - previous.time) /
                            duration)
                        : 0f;

                return Quaternion.Slerp(
                    ToQuaternion(previous),
                    ToQuaternion(next),
                    factor);
            }

            return ToQuaternion(
                keys[lastIndex]);
        }

        private static AnimationCurve CreateLinearCurve(
            List<ScalarSample> samples)
        {
            if (samples == null ||
                samples.Count == 0)
            {
                return new AnimationCurve();
            }

            Keyframe[] keys =
                new Keyframe[
                    samples.Count];

            for (int i = 0;
                 i < samples.Count;
                 ++i)
            {
                ScalarSample sample =
                    samples[i];

                float incoming =
                    i > 0
                        ? CalculateSlope(
                            samples[i - 1],
                            sample)
                        : 0f;

                float outgoing =
                    i + 1 < samples.Count
                        ? CalculateSlope(
                            sample,
                            samples[i + 1])
                        : incoming;

                if (i == 0)
                    incoming = outgoing;

                keys[i] =
                    new Keyframe(
                        sample.Time,
                        sample.Value,
                        incoming,
                        outgoing);
            }

            return new AnimationCurve(
                keys);
        }

        private static float CalculateSlope(
            ScalarSample from,
            ScalarSample to)
        {
            float duration =
                to.Time - from.Time;

            if (Mathf.Abs(duration) <=
                0.000001f)
            {
                return 0f;
            }

            return
                (to.Value - from.Value) /
                duration;
        }

        private static Vector3 ToVector3(
            PoseSequenceBakePositionKey key)
        {
            return new Vector3(
                key.x,
                key.y,
                key.z);
        }

        private static Quaternion ToQuaternion(
            PoseSequenceBakeRotationKey key)
        {
            Quaternion value =
                new Quaternion(
                    key.x,
                    key.y,
                    key.z,
                    key.w);

            float magnitude =
                Mathf.Sqrt(
                    value.x * value.x +
                    value.y * value.y +
                    value.z * value.z +
                    value.w * value.w);

            if (magnitude <=
                0.000001f)
            {
                return Quaternion.identity;
            }

            float inverse =
                1f / magnitude;

            return new Quaternion(
                value.x * inverse,
                value.y * inverse,
                value.z * inverse,
                value.w * inverse);
        }

        private static bool ValidateExchange(
            PoseSequenceBakeExchangeFile value,
            out string error)
        {
            error = string.Empty;

            if (value == null)
            {
                error = "JSON did not deserialize.";
                return false;
            }

            if (!string.Equals(
                    value.schema,
                    "PBAnimationLibrary.PoseSequenceBakeExchange",
                    StringComparison.Ordinal))
            {
                error =
                    "Unsupported schema: "
                    + value.schema;
                return false;
            }

            if (value.schemaVersion != 1)
            {
                error =
                    "Unsupported schema version: "
                    + value.schemaVersion;
                return false;
            }

            if (string.IsNullOrEmpty(
                    value.clipName))
            {
                error = "clipName is empty.";
                return false;
            }

            if (value.duration <= 0f)
            {
                error = "duration must be positive.";
                return false;
            }

            if (value.frameRate <= 0f)
            {
                error = "frameRate must be positive.";
                return false;
            }

            if (value.tracks == null ||
                value.tracks.Length == 0)
            {
                error = "No animated tracks.";
                return false;
            }

            for (int i = 0;
                 i < value.tracks.Length;
                 ++i)
            {
                PoseSequenceBakeTrack track =
                    value.tracks[i];

                if (track == null ||
                    string.IsNullOrEmpty(track.path))
                {
                    error =
                        "Track "
                        + i
                        + " has no path.";
                    return false;
                }

                if (track.hasPosition &&
                    (track.positionKeys == null ||
                     track.positionKeys.Length == 0))
                {
                    error =
                        "Position track has no keys: "
                        + track.path;
                    return false;
                }

                if (track.hasRotation &&
                    (track.rotationKeys == null ||
                     track.rotationKeys.Length == 0))
                {
                    error =
                        "Rotation track has no keys: "
                        + track.path;
                    return false;
                }
            }

            return true;
        }

        private static string NormalizeAssetFolder(
            string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            string normalized =
                value.Trim()
                    .Replace('\\', '/')
                    .TrimEnd('/');

            if (normalized == "Assets")
                return normalized;

            if (!normalized.StartsWith(
                    "Assets/",
                    StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return normalized;
        }

        private static void EnsureAssetFolder(
            string assetFolder)
        {
            string projectRoot =
                Directory.GetParent(
                    Application.dataPath)
                    .FullName;

            string fullPath =
                Path.Combine(
                    projectRoot,
                    assetFolder.Replace(
                        '/',
                        Path.DirectorySeparatorChar));

            Directory.CreateDirectory(
                fullPath);

            AssetDatabase.Refresh();
        }

        private static string GetUniqueAnimationAssetPath(
            string assetFolder,
            string baseName)
        {
            const int maximumAttempts = 9999;

            for (int index = 1;
                 index <= maximumAttempts;
                 ++index)
            {
                string candidate =
                    assetFolder
                    + "/"
                    + baseName
                    + "_"
                    + index.ToString("D3")
                    + ".anim";

                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(
                        candidate) == null)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException(
                "Failed to allocate a unique AnimationClip asset path.");
        }

        private static string SanitizeAssetName(
            string value)
        {
            string source =
                string.IsNullOrEmpty(value)
                    ? "pbalib_pose_sequence"
                    : value.Trim();

            char[] invalid =
                Path.GetInvalidFileNameChars();

            for (int i = 0;
                 i < invalid.Length;
                 ++i)
            {
                source =
                    source.Replace(
                        invalid[i],
                        '_');
            }

            source =
                source.Replace(
                    ' ',
                    '_');

            return source.Length > 0
                ? source
                : "pbalib_pose_sequence";
        }

        private static string SanitizeBundleName(
            string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            string source =
                value.Trim()
                    .ToLowerInvariant()
                    .Replace(
                        ' ',
                        '_')
                    .Replace(
                        '\\',
                        '/');

            return source;
        }

        private struct ScalarSample
        {
            internal readonly float Time;
            internal readonly float Value;

            internal ScalarSample(
                float time,
                float value)
            {
                Time = time;
                Value = value;
            }
        }
    }
}
