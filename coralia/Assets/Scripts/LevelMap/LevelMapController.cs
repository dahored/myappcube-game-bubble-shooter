using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelMapController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform contentRoot;
    [SerializeField] GameObject levelButtonPrefab;

    static readonly Color C_DONE   = new Color(0.18f, 0.72f, 0.65f);
    static readonly Color C_OPEN   = new Color(0.25f, 0.52f, 0.88f);
    static readonly Color C_LOCKED = new Color(0.18f, 0.22f, 0.32f);

    void Start() => BuildMap();

    void BuildMap()
    {
        var levels = LoadAllLevels();
        int maxUnlocked = SaveManager.MaxUnlockedLevel;

        // Group by chapter, sort chapters descending (newest first)
        var byChapter = new SortedDictionary<int, List<LevelData>>(Comparer<int>.Create((a, b) => b.CompareTo(a)));
        foreach (var lvl in levels)
        {
            if (!byChapter.ContainsKey(lvl.chapter))
                byChapter[lvl.chapter] = new List<LevelData>();
            byChapter[lvl.chapter].Add(lvl);
        }

        foreach (var chapter in byChapter)
        {
            // Chapter header
            AddChapterHeader(chapter.Key);

            // Levels within chapter: descending
            var chapterLevels = chapter.Value;
            chapterLevels.Sort((a, b) => b.id.CompareTo(a.id));

            foreach (var lvl in chapterLevels)
            {
                bool done   = lvl.id < maxUnlocked;
                bool open   = lvl.id == maxUnlocked;
                bool locked = lvl.id > maxUnlocked;
                AddLevelButton(lvl, done, open, locked);
            }
        }
    }

    void AddChapterHeader(int chapter)
    {
        var go = new GameObject($"Chapter_{chapter}_Header");
        go.transform.SetParent(contentRoot, false);
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = $"Capítulo {chapter}";
        txt.fontSize = 48;
        txt.color = new Color(0.6f, 0.85f, 1f);
        txt.alignment = TextAlignmentOptions.Center;
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1080f, 80f);
    }

    void AddLevelButton(LevelData lvl, bool done, bool open, bool locked)
    {
        var go = Instantiate(levelButtonPrefab, contentRoot);
        go.name = $"Level_{lvl.id}";

        var img = go.GetComponent<Image>();
        if (img) img.color = done ? C_DONE : open ? C_OPEN : C_LOCKED;

        var label = go.GetComponentInChildren<TextMeshProUGUI>();
        if (label) label.text = lvl.id.ToString();

        var btn = go.GetComponent<Button>();
        if (btn && !locked)
        {
            int id = lvl.id;
            btn.onClick.AddListener(() => OnLevelSelected(id));
        }
        else if (btn)
        {
            btn.interactable = false;
        }
    }

    void OnLevelSelected(int levelId)
    {
        PlayerPrefs.SetInt("selected_level", levelId);
        SceneLoader.GoTo(SceneLoader.GAMEPLAY);
    }

    List<LevelData> LoadAllLevels()
    {
        var result = new List<LevelData>();
        var jsons = Resources.LoadAll<TextAsset>("Levels");
        foreach (var json in jsons)
        {
            var lvl = JsonUtility.FromJson<LevelData>(json.text);
            if (lvl != null) result.Add(lvl);
        }
        return result;
    }
}
