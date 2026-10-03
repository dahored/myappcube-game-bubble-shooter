using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Una oferta del panel de recarga de boosters: icono, cuántos da, el precio y la etiqueta de
// descuento. Mismo reparto de responsabilidades que RefillLiveItem — el panel decide QUÉ mostrar,
// esta tarjeta solo lo presenta y avisa del toque.
public class RefillBoosterItemView : MonoBehaviour
{
    [SerializeField] Image      icon;
    [SerializeField] TMP_Text   quantityText;   // "x100"
    [SerializeField] TMP_Text   priceText;
    [SerializeField] Button     buyButton;

    [Tooltip("La etiqueta '50%'. Se apaga si esta oferta no tiene descuento.")]
    [SerializeField] GameObject discountCard;
    [SerializeField] TMP_Text   discountText;

    public event System.Action OnBuyClicked;

    void Awake()
    {
        ValidateReferences();
        if (buyButton) buyButton.onClick.AddListener(() => OnBuyClicked?.Invoke());
    }

    public void Setup(BoosterCatalog.Pack pack, Sprite boosterIcon, bool canAfford)
    {
        if (icon != null && boosterIcon != null) icon.sprite = boosterIcon;

        // Con la "x" delante y no suelto: el número es una cantidad, no un precio, y sin la marca
        // las dos cifras de la tarjeta se leen igual.
        if (quantityText) quantityText.text = $"x{pack.amount}";

        // El precio FINAL: el completo solo existe para calcularlo y para el tachado, si algún
        // día lo hay. Mostrar el de antes del descuento sería cobrar otro.
        if (priceText) priceText.text = pack.FinalPrice.ToString();

        // Deshabilitado si no alcanza: el tinte gris de Button.interactable es justo el feedback
        // que se busca acá.
        if (buyButton) buyButton.interactable = canAfford;

        bool hasDiscount = pack.discountPercent > 0;
        if (discountCard) discountCard.SetActive(hasDiscount);
        if (hasDiscount && discountText) discountText.text = $"{pack.discountPercent}%";
    }

    void ValidateReferences()
    {
        if (!icon)         Debug.LogWarning($"[RefillBoosterItemView] Falta asignar 'Icon' en '{name}'.", this);
        if (!quantityText) Debug.LogWarning($"[RefillBoosterItemView] Falta asignar 'Quantity Text' en '{name}'.", this);
        if (!priceText)    Debug.LogWarning($"[RefillBoosterItemView] Falta asignar 'Price Text' en '{name}'.", this);
        if (!buyButton)    Debug.LogWarning($"[RefillBoosterItemView] Falta asignar 'Buy Button' en '{name}' — la oferta no se puede comprar.", this);
    }
}
