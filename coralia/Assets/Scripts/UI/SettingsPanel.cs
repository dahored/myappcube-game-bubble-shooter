using UnityEngine;
using UnityEngine.UI;

public class SettingsPanel : UIPanel
{
    [Header("DEV — botones temporales, sacar antes de shippear (TempActions en el prefab)")]
    [SerializeField] Button resetTempButton;

    [Tooltip("Regala monedas para poder probar las compras mientras no exista la tienda (issue #57).")]
    [SerializeField] Button coinsTempButton;

    [SerializeField] int coinsPerTap = 10;

    protected override void Awake()
    {
        base.Awake();
        if (resetTempButton) resetTempButton.onClick.AddListener(ResetAllData);
        if (coinsTempButton) coinsTempButton.onClick.AddListener(GrantCoins);
    }

    // El panel se queda abierto: el que está probando suele querer varias tandas seguidas, y
    // cerrarse tras cada toque obligaría a reabrirlo cada vez.
    void GrantCoins()
    {
        SaveManager.Coins += coinsPerTap;
        Debug.Log($"[SettingsPanel] +{coinsPerTap} monedas. Saldo: {SaveManager.Coins}");
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
