using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Panel de confirmación reutilizable: "esto es lo que reclamaste/compraste" — vidas por ahora
// (único camino real, ya que la tienda de monedas todavía no existe, issue #57), monedas y
// boosters a futuro. El título queda fijo ("¡Listo!", ui.claim.panel.title vía LocalizedText
// en el Editor) — no cambia según qué se muestre, no hace falta tocarlo desde acá.
public class ClaimPanel : UIPanel
{
    [SerializeField] Button claimButton;
    [Tooltip("Espera antes de cerrar al tocar Claim, para que el pop de ButtonPop (~0.28s por default) alcance a jugarse completo — Close() por sí solo (closeDuration ~0.18s) lo cortaba a la mitad. Si cambiás la duración del pop en ButtonPop, ajustá esto acorde.")]
    [SerializeField] float  closeDelay = 0.3f;

    [Header("Zoom out del ítem reclamado al cerrar (visto en otros juegos)")]
    [SerializeField] float itemZoomOutDuration = 0.25f;
    [SerializeField] AnimationCurve itemZoomOutCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.4f, 1.1f),
        new Keyframe(1f, 0f, -2f, 0f));

    GameObject _activeItemRoot; // cuál de los 3 (lives/coins/shoots) está mostrado ahora — lo anima ShowOnly

    [Header("Vidas")]
    [SerializeField] GameObject livesItemRoot;   // ClaimItemLives
    [SerializeField] TMP_Text   totalVidasText;  // oculto en la variante infinita
    [SerializeField] GameObject infiniteIcon;    // el ∞ dentro del corazón — oculto en la variante numérica
    [SerializeField] GameObject timeRoot;        // badge de duración (ej. "3h") — solo tiene sentido en la infinita
    [SerializeField] TMP_Text   timeText;

    [Header("Monedas (sin uso todavía — depende de issue #57)")]
    [SerializeField] GameObject coinsItemRoot; // ClaimItemCoins
    [SerializeField] TMP_Text   totalCoinsText;

    [Header("Disparos extra (NoMoreMovesPanel)")]
    [SerializeField] GameObject shootsItemRoot; // ClaimItemShoots
    [SerializeField] TMP_Text   totalShootsText;

    protected override void Awake()
    {
        base.Awake();
        ValidateReferences();
        if (claimButton) claimButton.onClick.AddListener(HandleClaimClicked);
    }

    void HandleClaimClicked() => StartCoroutine(CloseAfterPop());

    IEnumerator CloseAfterPop()
    {
        yield return new WaitForSeconds(closeDelay);
        if (_activeItemRoot) yield return ZoomOutItem(_activeItemRoot);
        Close();
    }

    // 1 -> 1.1 -> 0 sobre el ítem reclamado en sí (no toda la card) — mismo efecto que se ve
    // en otros juegos al cerrar este tipo de panel, pedido de Diego.
    IEnumerator ZoomOutItem(GameObject item)
    {
        var t = item.transform;
        Vector3 baseScale = t.localScale;
        float time = 0f;
        while (time < itemZoomOutDuration)
        {
            time += Time.deltaTime;
            float p = itemZoomOutCurve.Evaluate(Mathf.Clamp01(time / itemZoomOutDuration));
            t.localScale = baseScale * p;
            yield return null;
        }
        t.localScale = Vector3.zero;
    }

    public void ShowLives(int total)
    {
        ShowOnly(livesItemRoot);
        if (totalVidasText) { totalVidasText.gameObject.SetActive(true); totalVidasText.text = total.ToString(); }
        if (infiniteIcon)   infiniteIcon.SetActive(false);
        if (timeRoot)       timeRoot.SetActive(false);
        Open();
    }

    // durationLabel: ej. "3h" — mismo formato que RefillLiveItem, se le pasa ya armado desde
    // RefillLivesPanel en vez de recalcularlo acá.
    public void ShowInfiniteLives(string durationLabel)
    {
        ShowOnly(livesItemRoot);
        if (totalVidasText) totalVidasText.gameObject.SetActive(false);
        if (infiniteIcon)   infiniteIcon.SetActive(true);
        if (timeRoot)       timeRoot.SetActive(true);
        if (timeText)       timeText.text = durationLabel;
        Open();
    }

    public void ShowCoins(int total)
    {
        ShowOnly(coinsItemRoot);
        if (totalCoinsText) totalCoinsText.text = total.ToString();
        Open();
    }

    // NoMoreMovesPanel — al comprar más disparos con monedas (GameplayController.OnContinuePressed).
    public void ShowShots(int total)
    {
        ShowOnly(shootsItemRoot);
        if (totalShootsText) totalShootsText.text = total.ToString();
        Open();
    }

    // Activa SOLO la variante pedida y apaga las otras dos — evita que cada Show* tenga que
    // acordarse de ocultar a mano cada vez que se agrega una variante nueva.
    void ShowOnly(GameObject target)
    {
        if (livesItemRoot)  livesItemRoot.SetActive(target == livesItemRoot);
        if (coinsItemRoot)  coinsItemRoot.SetActive(target == coinsItemRoot);
        if (shootsItemRoot) shootsItemRoot.SetActive(target == shootsItemRoot);

        // Reset por si quedó en escala 0 del ZoomOutItem de la vez anterior.
        if (target) target.transform.localScale = Vector3.one;
        _activeItemRoot = target;
    }

    void ValidateReferences()
    {
        if (!claimButton)    Debug.LogWarning("[ClaimPanel] Falta asignar 'Claim Button' en el Inspector.");
        if (!livesItemRoot)  Debug.LogWarning("[ClaimPanel] Falta asignar 'Lives Item Root' en el Inspector.");
        if (!totalVidasText) Debug.LogWarning("[ClaimPanel] Falta asignar 'Total Vidas Text' en el Inspector.");
        if (!infiniteIcon)   Debug.LogWarning("[ClaimPanel] Falta asignar 'Infinite Icon' en el Inspector.");
        if (!timeRoot)       Debug.LogWarning("[ClaimPanel] Falta asignar 'Time Root' en el Inspector.");
        if (!timeText)       Debug.LogWarning("[ClaimPanel] Falta asignar 'Time Text' en el Inspector.");
        if (!coinsItemRoot)  Debug.LogWarning("[ClaimPanel] Falta asignar 'Coins Item Root' en el Inspector.");
        if (!totalCoinsText) Debug.LogWarning("[ClaimPanel] Falta asignar 'Total Coins Text' en el Inspector.");
        if (!shootsItemRoot)  Debug.LogWarning("[ClaimPanel] Falta asignar 'Shoots Item Root' en el Inspector.");
        if (!totalShootsText) Debug.LogWarning("[ClaimPanel] Falta asignar 'Total Shoots Text' en el Inspector.");
    }
}
