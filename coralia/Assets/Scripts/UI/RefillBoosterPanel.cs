using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Modal de compra de boosters. Uno solo para los seis: lo que cambia entre un poder y otro es el
// icono, el texto y las ofertas, y todo eso sale de BoosterCatalog.
//
// Se abre directo, sin un paso previo tipo OutOfLivesPanel, porque quedarse sin un booster no
// bloquea nada: el jugador sigue jugando sin él. Ese intermedio existe en vidas porque ahí el
// juego SÍ se detiene y hay varias salidas que ofrecer.
public class RefillBoosterPanel : UIPanel
{
    [SerializeField] Button closeButton;

    [Header("Cabecera")]
    [SerializeField] Image    boosterIcon;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text descriptionText;

    [Tooltip("Las tarjetas de oferta, en orden. Las que sobren respecto a los packs del catálogo se apagan.")]
    [SerializeField] RefillBoosterItemView[] items;

    [Tooltip("La confirmación de compra. Opcional: sin esto la compra se hace igual, pero sin feedback.")]
    [SerializeField] ClaimPanel claimPanel;

    Booster _booster;

    protected override void Awake()
    {
        base.Awake();

        if (closeButton) closeButton.onClick.AddListener(Close);
        else Debug.LogWarning($"[RefillBoosterPanel] Falta asignar 'Close Button' en '{name}'.", this);

        // Por índice y no con un lambda que capture el bucle: cada tarjeta tiene que saber cuál
        // de los packs es suya, y el pack puede cambiar entre dos aperturas del panel.
        if (items != null)
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;
                int index = i;
                items[i].OnBuyClicked += () => Buy(index);
            }
    }

    public void Show(Booster booster)
    {
        _booster = booster;

        if (nameText)        nameText.text        = LocaleManager.Get(BoosterRules.NameKeyFor(booster));
        if (descriptionText) descriptionText.text = LocaleManager.Get(BoosterRules.DescriptionKeyFor(booster));

        var art = BoosterRules.IconFor(booster);
        if (boosterIcon != null && art != null) boosterIcon.sprite = art;

        Refresh();
        Open();
    }

    void Refresh()
    {
        var packs = Packs;
        var icon  = BoosterRules.IconFor(_booster);

        if (items == null) return;

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null) continue;

            bool hasPack = packs != null && i < packs.Length && packs[i] != null;
            items[i].gameObject.SetActive(hasPack);

            if (hasPack) items[i].Setup(packs[i], icon, SaveManager.Coins >= packs[i].FinalPrice);
        }
    }

    BoosterCatalog.Pack[] Packs => BoosterRules.ArtFor(_booster)?.packs;

    void Buy(int index)
    {
        var packs = Packs;
        if (packs == null || index < 0 || index >= packs.Length || packs[index] == null) return;

        var pack = packs[index];

        // Sin flujo de compra de monedas todavía (issue #57 — IAP): si no alcanza, no pasa nada
        // más que este aviso. El botón ya está deshabilitado, así que llegar acá sería un fallo.
        if (SaveManager.Coins < pack.FinalPrice)
        {
            Debug.LogWarning($"[RefillBoosterPanel] Monedas insuficientes para {pack.amount} de {_booster}.");
            return;
        }

        SaveManager.Coins -= pack.FinalPrice;
        SaveManager.GrantBooster(_booster, pack.amount);

        // Se repinta ANTES de cerrar: si el jugador vuelve a abrir el panel, los precios y el
        // "no te alcanza" ya están al día sin esperar a la próxima apertura.
        Refresh();

        // Y se cierra para dejar paso a la confirmación, igual que RefillLivesPanel. Las dos
        // animaciones se solapan un instante y se ve bien: una entra mientras la otra sale.
        if (claimPanel == null) return;

        Close();
        claimPanel.ShowBooster(BoosterRules.IconFor(_booster), pack.amount);
    }
}
