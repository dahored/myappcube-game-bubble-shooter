using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Componente tonto y reutilizable: ícono + cantidad. Usado en la fila de recompensas del
// WinPanel (monedas, gemas, etc. — GDD §6.3-6.4), pero sin lógica de economía propia.
public class AwardItem : MonoBehaviour
{
    [SerializeField] Image    icon;
    [SerializeField] TMP_Text valueText;

    public void Set(Sprite iconSprite, int amount)
    {
        if (icon) icon.sprite = iconSprite;
        if (valueText) valueText.text = amount.ToString();
    }
}
