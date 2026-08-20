using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tres estados sobre la misma Card: victoria (siempre "Continuar" → mapa), oferta de
// seguir jugando pagando gemas (GDD §7), y confirmación de abandono (-1 vida) al tocar
// la X sobre la oferta. GameplayController escucha los eventos y decide qué pasa en el
// juego (revivir o restar vida) — este panel solo maneja presentación.
public class WinLosePanel : UIPanel
{
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text messageText;
    [SerializeField] TMP_Text primaryButtonText;
    [SerializeField] Button   primaryButton;
    [SerializeField] Button   closeButton;

    public const int ContinueGemsCost = 15; // GDD §7 — "Pagar gemas: +5 disparos, 15 gemas"

    public event System.Action OnContinuePressed;
    public event System.Action OnAbandonPressed;

    enum State { Win, LoseOffer, AbandonConfirm }
    State _state;

    protected override void Awake()
    {
        base.Awake();
        primaryButton.onClick.AddListener(OnPrimaryClicked);
        closeButton.onClick.AddListener(ShowAbandonConfirm);
    }

    public void ShowWin(bool creatureRescued)
    {
        _state = State.Win;
        titleText.text         = LocaleManager.Get("ui.victory.title");
        messageText.text       = creatureRescued ? LocaleManager.Get("ui.victory.creature_rescued") : "";
        primaryButtonText.text = LocaleManager.Get("ui.victory.continue");
        closeButton.gameObject.SetActive(false);
        Open();
    }

    public void ShowLose()
    {
        _state = State.LoseOffer;
        titleText.text         = LocaleManager.Get("ui.gameover.title");
        messageText.text       = "";
        primaryButtonText.text = LocaleManager.Get("ui.gameover.continue_gems").Replace("{gems}", ContinueGemsCost.ToString());
        closeButton.gameObject.SetActive(true);
        Open();
    }

    void ShowAbandonConfirm()
    {
        _state                  = State.AbandonConfirm;
        messageText.text        = LocaleManager.Get("ui.gameover.confirm_abandon");
        primaryButtonText.text  = LocaleManager.Get("ui.gameover.accept");
        closeButton.gameObject.SetActive(false);
    }

    void OnPrimaryClicked()
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
