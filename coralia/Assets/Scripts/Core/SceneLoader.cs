using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneLoader
{
    // SPLASH SCREENS
    public const string SPLASH_STUDIO  = "SplashStudio";
    public const string SPLASH_GAME    = "SplashGame";

    // HOME SCREENS
    public const string HOME_GAME       = "HomeGame";
    public const string SETTINGS        = "Settings";

    // GAME SCREENS
    public const string GAMEPLAY       = "Gameplay";

    // El mapa del juego es el curvo (mundo 3D sobre una esfera): ahí van el botón de jugar del
    // Home y todos los regresos desde Gameplay (WinPanel, LosePanel, QuitPanel).
    public const string LEVEL_MAP = "LevelMapCurve";

    // El mapa plano original, que ya no se navega. Se conserva porque es la única referencia
    // funcionando de varias cosas que el curvo todavía resuelve distinto, y para poder comparar
    // comportamiento. Para volver a él alcanza con apuntar LEVEL_MAP acá y marcar la escena en
    // File -> Build Profiles.
    public const string LEVEL_MAP_FLAT = "LevelMap";

    public static void GoTo(string sceneName) => SceneTransition.GoTo(sceneName);
}
