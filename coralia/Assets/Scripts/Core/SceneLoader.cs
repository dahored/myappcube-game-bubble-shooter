using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneLoader
{
    public const string SPLASH_STUDIO  = "SplashStudio";
    public const string SPLASH_GAME    = "SplashGame";
    public const string LEVEL_MAP      = "LevelMap";
    public const string GAMEPLAY       = "Gameplay";

    public static void GoTo(string sceneName) => SceneTransition.GoTo(sceneName);
}
