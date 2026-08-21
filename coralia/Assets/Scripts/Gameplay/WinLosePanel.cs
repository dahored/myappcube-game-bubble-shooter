using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tres estados sobre la misma Card: victoria (gemsButton = "Continuar" → mapa, adButton
// oculto), oferta de seguir jugando (gemsButton paga gemas, adButton ve un anuncio — GDD
// §7, deshabilitado hasta tener el SDK de ads, issue #14), y confirmación de abandono
// (-1 vida) al tocar la X sobre la oferta. GameplayController escucha los eventos y
// decide qué pasa en el juego (revivir o restar vida) — este panel solo maneja presentación.
public class WinLosePanel : UIPanel
{
    [SerializeField] TMP_Text   titleText;
    [SerializeField] MessageView messageView;
    [SerializeField] TMP_Text   gemsButtonText;
    [SerializeField] Button     gemsButton;
    [SerializeField] Button     adButton;
    [SerializeField] Button     closeButton;

    [Header("Íconos por estado (opcional — dejar vacío hasta tener el arte)")]
    [SerializeField] Sprite winIcon;
    [SerializeField] Sprite loseIcon;
    [SerializeField] Sprite abandonIcon;

    public const int ContinueGemsCost = 15; // GDD §7 — "Pagar gemas: +5 disparos, 15 gemas"

    public event System.Action OnContinuePressed;
    public event System.Action OnAbandonPressed;

    enum State { Win, LoseOffer, AbandonConfirm }
    State _state;

    protected override void Awake()
    {
        base.Awake();
        gemsButton.onClick.AddListener(OnGemsClicked);
        closeButton.onClick.AddListener(ShowAbandonConfirm);
        adButton.interactable = false; // sin SDK de ads todavía — issue #14
    }

    public void ShowWin(bool creatureRescued)
    {
        _state = State.Win;
        titleText.text      = LocaleManager.Get("ui.victory.title");
        messageView.Set(winIcon, creatureRescued ? LocaleManager.Get("ui.victory.creature_rescued") : "");
        gemsButtonText.text = LocaleManager.Get("ui.victory.continue");
        adButton.gameObject.SetActive(false);
        closeButton.gameObject.SetActive(false);
        Open();
    }

    public void ShowLose()
    {
        _state = State.LoseOffer;
        titleText.text      = LocaleManager.Get("ui.gameover.title");
        messageView.Set(loseIcon, "");
        gemsButtonText.text = LocaleManager.Get("ui.gameover.continue_gems").Replace("{gems}", ContinueGemsCost.ToString());
        adButton.gameObject.SetActive(true);
        closeButton.gameObject.SetActive(true);
        Open();
    }

    void ShowAbandonConfirm()
    {
        _state               = State.AbandonConfirm;
        messageView.Set(abandonIcon, LocaleManager.Get("ui.gameover.confirm_abandon"));
        gemsButtonText.text  = LocaleManager.Get("ui.gameover.accept");
        adButton.gameObject.SetActive(false);
        closeButton.gameObject.SetActive(false);
    }

    void OnGemsClicked()
    {
        switch (_state)
        {
            case State.Win:
                SceneLoader.GoTo(SceneLoader.LEVEL_MAP);
                break;
            case State.LoseOffer:
                OnContinuePressed?.Invoke();
                break;
            case State.AbandonConfirm:
                OnAbandonPressed?.Invoke();
                SceneLoader.GoTo(SceneLoader.LEVEL_MAP);
                break;
        }
    }
}
