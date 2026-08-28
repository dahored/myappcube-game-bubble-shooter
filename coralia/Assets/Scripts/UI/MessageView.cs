using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Componente tonto y reutilizable: ícono + texto. No sabe nada del estado que lo llama
// (victoria, derrota, logro, etc.) — eso lo decide quien lo usa, vía Set().
public class MessageView : MonoBehaviour
{
    [SerializeField] Image    icon;
    [SerializeField] TMP_Text text;

    public void Set(Sprite iconSprite, string message)
    {
        if (icon)
        {
            icon.sprite = iconSprite;
            icon.gameObject.SetActive(iconSprite != null);
        }
        if (text) text.text = message;
    }
}
