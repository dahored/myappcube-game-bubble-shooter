using UnityEngine;
using UnityEngine.UI;

public class SettingsPanel : UIPanel
{
    [Header("DEV — botón temporal, sacar antes de shippear (ver ResetTempButton en el prefab)")]
    [SerializeField] Button resetTempButton;

    protected override void Awake()
    {
        base.Awake();
        if (resetTempButton) resetTempButton.onClick.AddListener(ResetAllData);
    }

    // Borra todo PlayerPrefs (monedas, vidas, idioma, progreso de niveles, audio, todo) y
    // reinicia el flujo desde Splash — simula una instalación nueva sin desinstalar la app
    // de verdad. Diego lo pidió como botón temporal mientras desarrolla, no es para producción.
    void ResetAllData()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        SceneLoader.GoTo(SceneLoader.SPLASH_STUDIO);
    }
}
