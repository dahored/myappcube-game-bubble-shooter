using UnityEngine;
using UnityEngine.UI;

// Menú de pausa. Resume, Settings, Restart y Quit ya abren/cierran correctamente. El "Yes"
// de ResetProgressPanel y QuitPanel todavía no dispara ninguna acción real — falta definir
// qué reinicia exactamente uno y a dónde navega el otro.
public class PausedPanel : UIPanel
{
    [SerializeField] Button             resumeButton;
    [SerializeField] Button             restartButton;
    [SerializeField] Button             settingsButton;
    [SerializeField] Button             quitButton;
    [SerializeField] SettingsPanel      settingsPanel;
    [SerializeField] ResetProgressPanel resetProgressPanel;
    [SerializeField] QuitPanel          quitPanel;

    public event System.Action OnResumePressed;

    protected override void Awake()
    {
        base.Awake();
        ValidateReferences();
        if (resumeButton)   resumeButton.onClick.AddListener(HandleResume);
        if (settingsButton) settingsButton.onClick.AddListener(() => OpenSubPanel(settingsPanel, "Settings Panel"));
        if (restartButton)  restartButton.onClick.AddListener(() => OpenSubPanel(resetProgressPanel, "Reset Progress Panel"));
        if (quitButton)      quitButton.onClick.AddListener(() => OpenSubPanel(quitPanel, "Quit Panel"));
    }

    void HandleResume()
    {
        Close();
        OnResumePressed?.Invoke();
    }

    // Patrón compartido por Settings/Restart (y probablemente Quit): este panel se cierra,
    // abre `panel`, y cuando ESE se cierra a su vez (con su propia X o botón, sea cual sea
    // el camino), este panel vuelve a abrirse solo — ver UIPanel.OnClosed.
    void OpenSubPanel(UIPanel panel, string missingFieldLabel)
    {
        if (!panel)
        {
            Debug.LogWarning($"[PausedPanel] Falta asignar '{missingFieldLabel}' en el Inspector.");
            return;
        }

        Close();
        panel.Open();
        panel.OnClosed += ReturnFromSubPanel;

        void ReturnFromSubPanel()
        {
            panel.OnClosed -= ReturnFromSubPanel;
            Open();
        }
    }

    void ValidateReferences()
    {
        if (!resumeButton)         Debug.LogWarning("[PausedPanel] Falta asignar 'Resume Button' en el Inspector.");
        if (!restartButton)        Debug.LogWarning("[PausedPanel] Falta asignar 'Restart Button' en el Inspector.");
        if (!settingsButton)       Debug.LogWarning("[PausedPanel] Falta asignar 'Settings Button' en el Inspector.");
        if (!quitButton)           Debug.LogWarning("[PausedPanel] Falta asignar 'Quit Button' en el Inspector.");
        if (!settingsPanel)        Debug.LogWarning("[PausedPanel] Falta asignar 'Settings Panel' en el Inspector.");
        if (!resetProgressPanel)   Debug.LogWarning("[PausedPanel] Falta asignar 'Reset Progress Panel' en el Inspector.");
        if (!quitPanel)            Debug.LogWarning("[PausedPanel] Falta asignar 'Quit Panel' en el Inspector.");
    }
}
