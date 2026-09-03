using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Pantalla previa a Gameplay (issue #12): banner de nivel + objetivo + selección de
// boosters (todavía decorativa — el sistema de power-ups no existe, ver issues #23/#2) +
// botón Play, que recién ahí navega a Gameplay. LevelMapController la abre en vez de
// navegar directo al tocar un nodo — Close cancela y se queda en el mapa.
public class StartGamePanel : UIPanel
{
    [SerializeField] TMP_Text levelBannerText;
    [SerializeField] TMP_Text objectiveText;
    [SerializeField] Button   playButton;
    [SerializeField] Button   closeButton;

    [Header("Ícono según el objetivo (level.objective.type)")]
    [SerializeField] Image  objectiveIcon;
    [SerializeField] Sprite clearAllIcon;
    [SerializeField] Sprite rescueIcon; // genérico por ahora — un ícono por criatura queda pendiente del arte (issues #25/#37)

    int _levelId;

    protected override void Awake()
    {
        base.Awake();
        ValidateReferences();
        if (playButton)  playButton.onClick.AddListener(PlaySelectedLevel);
        if (closeButton) closeButton.onClick.AddListener(Close);
    }

    public void Show(LevelData level)
    {
        _levelId = level.id;

        if (levelBannerText) levelBannerText.text = LocaleManager.Get("ui.victory.level_banner").Replace("{id}", level.id.ToString());

        bool isRescue = level.objective != null && level.objective.type == "rescue";

        if (objectiveText)
        {
            string key = isRescue ? "ui.gameplay.objective.rescue" : "ui.gameplay.objective.clear_all";
            objectiveText.text = LocaleManager.Get(key).Replace("{creature}", level.objective?.creature_id ?? "");
        }

        if (objectiveIcon) objectiveIcon.sprite = isRescue ? rescueIcon : clearAllIcon;

        Open();
    }

    void PlaySelectedLevel()
    {
        PlayerPrefs.SetInt("selected_level", _levelId);
        SceneLoader.GoTo(SceneLoader.GAMEPLAY);
    }

    void ValidateReferences()
    {
        if (!playButton)  Debug.LogWarning("[StartGamePanel] Falta asignar 'Play Button' en el Inspector.");
        if (!closeButton) Debug.LogWarning("[StartGamePanel] Falta asignar 'Close Button' en el Inspector.");
    }
}
