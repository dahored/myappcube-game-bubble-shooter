using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public static class CoraliaSetup
{
    static readonly Color BG_COLOR  = new Color(0.04f, 0.08f, 0.15f, 1f);
    const string SCENES_PATH = "Assets/Scenes";

    // ─────────────────────────────────────────────
    [MenuItem("Coralia/Setup — Create All Scenes")]
    public static void SetupAll()
    {
        ConfigureSpriteImports();
        MoveJsonsToResources();
        CreateSplashStudio();
        CreateSplashGame();
        CreateLevelMap();
        SetBuildScenes();
        AssetDatabase.Refresh();
        Debug.Log("[Coralia] Setup complete. Check the Scenes folder.");
    }

    // ─────────────────────────────────────────────
    // SPRITES
    // ─────────────────────────────────────────────
    static void ConfigureSpriteImports()
    {
        string[] folders = { "Assets/Sprites/Logos", "Assets/Sprites/Bubbles" };
        foreach (var folder in folders)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.SaveAndReimport();
                }
            }
        }
    }

    // ─────────────────────────────────────────────
    // JSON levels → Resources/Levels
    // ─────────────────────────────────────────────
    static void MoveJsonsToResources()
    {
        var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/Data/Levels" });
        foreach (var guid in guids)
        {
            var src = AssetDatabase.GUIDToAssetPath(guid);
            var filename = System.IO.Path.GetFileName(src);
            var dst = $"Assets/Resources/Levels/{filename}";
            if (!AssetDatabase.LoadAssetAtPath<TextAsset>(dst))
            {
                System.IO.Directory.CreateDirectory("Assets/Resources/Levels");
                AssetDatabase.CopyAsset(src, dst);
            }
        }
    }

    // ─────────────────────────────────────────────
    // SCENE HELPERS
    // ─────────────────────────────────────────────
    static GameObject MakeCamera()
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BG_COLOR;
        cam.orthographic = true;
        go.AddComponent<AudioListener>();
        return go;
    }

    static GameObject MakeCanvas()
    {
        var go = new GameObject("Canvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return go;
    }

    static GameObject MakeEventSystem()
    {
        var go = new GameObject("EventSystem");
        go.AddComponent<UnityEngine.EventSystems.EventSystem>();
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        return go;
    }

    static RectTransform StretchFill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static Image MakeBG(Transform parent)
    {
        var go = new GameObject("BG");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = BG_COLOR;
        StretchFill(go.GetComponent<RectTransform>());
        return img;
    }

    static Sprite LoadSprite(string nameFilter, string folder = "Assets/Sprites/Logos")
    {
        var guids = AssetDatabase.FindAssets($"{nameFilter} t:Sprite", new[] { folder });
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    static void SaveAndOpen(string path)
    {
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), path);
        Debug.Log($"[Coralia] Saved {path}");
    }

    // ─────────────────────────────────────────────
    // SPLASH 1 — Studio
    // ─────────────────────────────────────────────
    static void CreateSplashStudio()
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>($"{SCENES_PATH}/SplashStudio.unity") != null)
        { Debug.Log("[Coralia] SplashStudio already exists — skipped."); return; }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        MakeCamera();
        var canvas = MakeCanvas();
        MakeEventSystem();
        MakeBG(canvas.transform);

        // Logo image — centered, 200x200
        var logoGO = new GameObject("Logo");
        logoGO.transform.SetParent(canvas.transform, false);
        var logoImg = logoGO.AddComponent<Image>();
        logoImg.preserveAspect = true;
        logoImg.color = new Color(1, 1, 1, 0);
        logoImg.sprite = LoadSprite("logo_myappcube");
        var lr = logoGO.GetComponent<RectTransform>();
        lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0.5f, 0.5f);
        lr.sizeDelta = new Vector2(200, 200);
        lr.anchoredPosition = Vector2.zero;

        // Studio name label — "myapp" white + "cube" purple
        var labelGO = new GameObject("StudioLabel");
        labelGO.transform.SetParent(canvas.transform, false);
        var labelTxt = labelGO.AddComponent<TextMeshProUGUI>();
        labelTxt.richText = true;
        labelTxt.text = "<color=#FFFFFF>myapp</color><color=#A076F0>cube</color>";
        labelTxt.fontSize = 72;
        labelTxt.fontStyle = FontStyles.Bold;
        labelTxt.alignment = TextAlignmentOptions.Center;
        labelTxt.color = new Color(1, 1, 1, 0);
        var labelFontGuids = AssetDatabase.FindAssets("Poppins-Bold SDF t:TMP_FontAsset", new[] { "Assets/Fonts" });
        if (labelFontGuids.Length > 0)
        {
            var fp = AssetDatabase.GUIDToAssetPath(labelFontGuids[0]);
            var fnt = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(fp);
            if (fnt != null) labelTxt.font = fnt;
        }
        var labelRt = labelGO.GetComponent<RectTransform>();
        labelRt.anchorMin = labelRt.anchorMax = labelRt.pivot = new Vector2(0.5f, 0.5f);
        labelRt.sizeDelta = new Vector2(600, 100);
        labelRt.anchoredPosition = new Vector2(0, -150);

        // Controller
        var ctrl = new GameObject("SplashController");
        var script = ctrl.AddComponent<SplashStudio>();
        var so = new SerializedObject(script);
        so.FindProperty("logoImage").objectReferenceValue = logoImg;
        so.FindProperty("studioLabel").objectReferenceValue = labelTxt;
        so.ApplyModifiedProperties();

        SaveAndOpen($"{SCENES_PATH}/SplashStudio.unity");
    }

    // ─────────────────────────────────────────────
    // SPLASH 2 — Game logo + loading
    // ─────────────────────────────────────────────
    static void CreateSplashGame()
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>($"{SCENES_PATH}/SplashGame.unity") != null)
        { Debug.Log("[Coralia] SplashGame already exists — skipped."); return; }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        MakeCamera();
        var canvas = MakeCanvas();
        MakeEventSystem();
        MakeBG(canvas.transform);

        // Logo — upper center
        var logoGO = new GameObject("Logo");
        logoGO.transform.SetParent(canvas.transform, false);
        var logoImg = logoGO.AddComponent<Image>();
        logoImg.preserveAspect = true;
        logoImg.color = new Color(1, 1, 1, 0);
        logoImg.sprite = LoadSprite("logo");
        var lr = logoGO.GetComponent<RectTransform>();
        lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0.5f, 0.5f);
        lr.sizeDelta = new Vector2(200, 200);
        lr.anchoredPosition = new Vector2(0, 290);  // above center

        // Version label
        var versionGO = new GameObject("VersionLabel");
        versionGO.transform.SetParent(canvas.transform, false);
        var versionTxt = versionGO.AddComponent<TextMeshProUGUI>();
        versionTxt.text = "v0.1.0";
        versionTxt.fontSize = 32;
        versionTxt.alignment = TextAlignmentOptions.Center;
        versionTxt.color = new Color(0.5f, 0.7f, 0.85f, 0);
        var vr = versionGO.GetComponent<RectTransform>();
        vr.anchorMin = vr.anchorMax = vr.pivot = new Vector2(0.5f, 0.5f);
        vr.sizeDelta = new Vector2(400, 60);
        vr.anchoredPosition = new Vector2(0, -260);

        // Loading label
        var loadGO = new GameObject("LoadingLabel");
        loadGO.transform.SetParent(canvas.transform, false);
        var loadTxt = loadGO.AddComponent<TextMeshProUGUI>();
        loadTxt.text = "Cargando...";
        loadTxt.fontSize = 32;
        loadTxt.alignment = TextAlignmentOptions.Center;
        loadTxt.color = new Color(0.5f, 0.7f, 0.85f, 0);
        var ldr = loadGO.GetComponent<RectTransform>();
        ldr.anchorMin = ldr.anchorMax = ldr.pivot = new Vector2(0.5f, 0.5f);
        ldr.sizeDelta = new Vector2(400, 60);
        ldr.anchoredPosition = new Vector2(0, -780);

        // Controller
        var ctrl = new GameObject("SplashController");
        var script = ctrl.AddComponent<SplashGame>();
        var so = new SerializedObject(script);
        so.FindProperty("logoImage").objectReferenceValue = logoImg;
        so.FindProperty("versionLabel").objectReferenceValue = versionTxt;
        so.FindProperty("loadingLabel").objectReferenceValue = loadTxt;
        so.ApplyModifiedProperties();

        SaveAndOpen($"{SCENES_PATH}/SplashGame.unity");
    }

    // ─────────────────────────────────────────────
    // LEVEL MAP
    // ─────────────────────────────────────────────
    static void CreateLevelMap()
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>($"{SCENES_PATH}/LevelMap.unity") != null)
        { Debug.Log("[Coralia] LevelMap already exists — skipped."); return; }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        MakeCamera();
        var canvas = MakeCanvas();
        MakeEventSystem();
        MakeBG(canvas.transform);

        // Header
        var headerGO = new GameObject("Header");
        headerGO.transform.SetParent(canvas.transform, false);
        var headerTxt = headerGO.AddComponent<TextMeshProUGUI>();
        headerTxt.text = "Coralia";
        headerTxt.fontSize = 72;
        headerTxt.fontStyle = FontStyles.Bold;
        headerTxt.alignment = TextAlignmentOptions.Center;
        headerTxt.color = new Color(0.6f, 0.85f, 1f);
        var hr = headerGO.GetComponent<RectTransform>();
        hr.anchorMin = new Vector2(0, 1); hr.anchorMax = new Vector2(1, 1);
        hr.pivot = new Vector2(0.5f, 1f);
        hr.offsetMin = new Vector2(0, -120); hr.offsetMax = Vector2.zero;

        // ScrollView
        var scrollGO = new GameObject("ScrollView");
        scrollGO.transform.SetParent(canvas.transform, false);
        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        var scrollRt = scrollGO.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0, 0);
        scrollRt.anchorMax = new Vector2(1, 1);
        scrollRt.offsetMin = new Vector2(0, 0);
        scrollRt.offsetMax = new Vector2(0, -120);

        // Viewport
        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollGO.transform, false);
        var vpMask = viewportGO.AddComponent<RectMask2D>();
        var vpRt = viewportGO.GetComponent<RectTransform>();
        StretchFill(vpRt);
        scrollRect.viewport = vpRt;

        // Content
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform, false);
        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 20;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(20, 20, 20, 20);
        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var contentRt = contentGO.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.offsetMin = contentRt.offsetMax = Vector2.zero;
        scrollRect.content = contentRt;

        // Level button prefab
        var prefab = CreateLevelButtonPrefab();

        // Controller
        var ctrl = new GameObject("LevelMapController");
        var script = ctrl.AddComponent<LevelMapController>();
        var so = new SerializedObject(script);
        so.FindProperty("contentRoot").objectReferenceValue = contentRt;
        so.FindProperty("levelButtonPrefab").objectReferenceValue = prefab;
        so.ApplyModifiedProperties();

        SaveAndOpen($"{SCENES_PATH}/LevelMap.unity");
    }

    static GameObject CreateLevelButtonPrefab()
    {
        string prefabPath = "Assets/Prefabs/UI/LevelButton.prefab";

        var go = new GameObject("LevelButton");

        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.52f, 0.88f);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 180);

        go.AddComponent<Button>();

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var txt = labelGO.AddComponent<TextMeshProUGUI>();
        txt.text = "1";
        txt.fontSize = 48;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        var lr = labelGO.GetComponent<RectTransform>();
        StretchFill(lr);

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ─────────────────────────────────────────────
    // BUILD SETTINGS
    // ─────────────────────────────────────────────
    static void SetBuildScenes()
    {
        var scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene($"{SCENES_PATH}/SplashStudio.unity", true),
            new EditorBuildSettingsScene($"{SCENES_PATH}/SplashGame.unity",   true),
            new EditorBuildSettingsScene($"{SCENES_PATH}/LevelMap.unity",     true),
        };
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[Coralia] Build Settings updated.");
    }
}
