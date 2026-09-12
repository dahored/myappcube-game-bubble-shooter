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

    // TEMPORAL — mientras se prototipa el mapa curvo en 3D, TODO el juego navega ahí: el botón
    // de jugar del Home y los regresos desde Gameplay (WinPanel, LosePanel, QuitPanel).
    // Para volver al mapa real, esta línea pasa a "LevelMap" y hay que marcar la escena en
    // File -> Build Profiles. Es el único lugar que hay que tocar.
    public const string LEVEL_MAP      = "LevelMapCurve";
    public const string LEVEL_MAP_REAL = "LevelMap";

    public static void GoTo(string sceneName) => SceneTransition.GoTo(sceneName);
}
