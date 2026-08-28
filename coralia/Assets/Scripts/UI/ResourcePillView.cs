using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResourcePillView : MonoBehaviour
{
    [SerializeField] Image    iconImage;
    [SerializeField] TMP_Text valueText;
    [SerializeField] Button   plusButton;

    [Header("Badge (opcional — ej. cantidad de vidas sobre el ícono)")]
    [SerializeField] GameObject badgeRoot;
    [SerializeField] TMP_Text   badgeText;
    [SerializeField] GameObject badgeInfinite;

    public event Action OnPlusClicked;

    void Awake()
    {
        if (plusButton) plusButton.onClick.AddListener(() => OnPlusClicked?.Invoke());
    }

    public void SetIcon(Sprite sprite)
    {
        if (iconImage) iconImage.sprite = sprite;
    }

    // Para instancias donde el "+" no debe interferir (ej. NoMoreMovesPanel, que ya tiene
    // su propio botón de compra) — no afecta otras instancias de este prefab en la escena.
    public void SetPlusVisible(bool visible)
    {
        if (plusButton) plusButton.gameObject.SetActive(visible);
    }

    public void SetValue(int value)
    {
        if (valueText) valueText.text = value.ToString();
    }

    public void SetFull()
    {
        if (valueText) valueText.text = LocaleManager.Get("ui.hud.full");
    }

    public void SetTimer(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        if (valueText) valueText.text = $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
    }

    public void SetInfinite()
    {
        if (valueText) valueText.text = "∞";
    }

    public void SetBadge(string text)
    {
        if (badgeRoot) badgeRoot.SetActive(true);
        if (badgeText) badgeText.text = text;
        if (badgeInfinite) badgeInfinite.SetActive(false);
    }

    public void SetBadgeInfinite()
    {
        if (badgeRoot) badgeRoot.SetActive(false);
        if (badgeInfinite) badgeInfinite.SetActive(true);
    }

    public void HideBadge()
    {
        if (badgeRoot) badgeRoot.SetActive(false);
        if (badgeInfinite) badgeInfinite.SetActive(false);
    }
}
