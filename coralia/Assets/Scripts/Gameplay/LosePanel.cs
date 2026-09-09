using UnityEngine;
using UnityEngine.UI;

// Pantalla final de derrota ("Nivel fallido"). Se abre cuando el jugador cierra
// NoMoreMovesPanel sin pagar — la vida ya se descontó ahí (ver
// GameplayController.OnDeclined), así que este panel no toca SaveManager, solo navega:
// Try again reintenta el mismo nivel, la X vuelve al mapa. Todos los textos son estáticos
// -> LocalizedText en el Editor, no hace falta código para setearlos.
public class LosePanel : UIPanel
{
    [SerializeField] Button tryButton;
    [SerializeField] Button closeButton;
    [Tooltip("Si al tocar Try Again no quedan vidas (la última ya se descontó al abrir este panel), se abre esto en vez de recargar la escena — evita la transición de burbujas para terminar mostrando el mismo aviso de todos modos.")]
    [SerializeField] OutOfLivesPanel outOfLivesPanel;

    [Header("Sonido (opcional — dejar vacío hasta tener el clip)")]
    [SerializeField] AudioClip loseClip; // suena apenas se abre el panel — mismo criterio que WinPanel.winClip
    [SerializeField, Range(0f, 1f)] float loseClipVolume = 0.6f; // mismo default que winClipVolume, ajustable si suena "duro"

    protected override void Awake()
    {
        base.Awake();
        ValidateReferences();
        if (tryButton)   tryButton.onClick.AddListener(Retry);
        if (closeButton) closeButton.onClick.AddListener(GoToMap);
    }

    public override void Open()
    {
        base.Open();
        AudioManager.Instance?.StopMusic(); // corta la música de gameplay al perder (pedido de Diego) — así se escucha bien el sonido de derrota
        AudioManager.Instance?.PlayUi(loseClip, loseClipVolume);
    }

    // La vida de esta derrota ya se descontó antes de que este panel se abriera (ver
    // GameplayController.ShowRealLoss) — así que si SaveManager.HasLivesAvailable ya es
    // false acá, reintentar no tiene sentido: mostramos el aviso de una, sin recargar la
    // escena primero solo para que el guard de Start() la vuelva a mostrar igual.
    void Retry()
    {
        if (!SaveManager.HasLivesAvailable)
        {
            Close();
            OpenOutOfLives();
            return;
        }
        SceneLoader.GoTo(SceneLoader.GAMEPLAY);
    }

    void OpenOutOfLives()
    {
        if (!outOfLivesPanel)
        {
            Debug.LogWarning("[LosePanel] Falta asignar 'Out Of Lives Panel' en el Inspector — no se puede avisar sin vidas.");
            return;
        }

        outOfLivesPanel.OnCloseButtonPressed += GoToMap;
        outOfLivesPanel.OnResolved           += RetryAfterResolved;
        outOfLivesPanel.Open();

        void RetryAfterResolved()
        {
            outOfLivesPanel.OnCloseButtonPressed -= GoToMap;
            outOfLivesPanel.OnResolved           -= RetryAfterResolved;
            SceneLoader.GoTo(SceneLoader.GAMEPLAY); // ya hay vidas/boost — recién ahora tiene sentido reintentar
        }
    }

    void GoToMap() => SceneLoader.GoTo(SceneLoader.LEVEL_MAP);

    void ValidateReferences()
    {
        if (!tryButton)       Debug.LogWarning("[LosePanel] Falta asignar 'Try Button' en el Inspector.");
        if (!closeButton)     Debug.LogWarning("[LosePanel] Falta asignar 'Close Button' en el Inspector.");
        if (!outOfLivesPanel) Debug.LogWarning("[LosePanel] Falta asignar 'Out Of Lives Panel' en el Inspector.");
    }
}
