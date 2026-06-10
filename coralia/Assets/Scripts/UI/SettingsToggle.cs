using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsToggle : MonoBehaviour
{
    [SerializeField] TMP_Text labelText;
    [SerializeField] Toggle   toggle;

    [Header("Switch sprites")]
    [SerializeField] Image  switchImage;
    [SerializeField] Sprite spriteOn;
    [SerializeField] Sprite spriteOff;

    void Awake()
    {
        if (toggle) toggle.onValueChanged.AddListener(OnToggleChanged);
        RefreshSprite(toggle ? toggle.isOn : false);
    }

    public string Label
    {
        get => labelText ? labelText.text : "";
        set { if (labelText) labelText.text = value; }
    }

    public bool Value
    {
        get => toggle && toggle.isOn;
        set
        {
            if (!toggle) return;
            toggle.SetIsOnWithoutNotify(value);
            RefreshSprite(value);
        }
    }

    public void AddListener(UnityEngine.Events.UnityAction<bool> action)
    {
        if (toggle) toggle.onValueChanged.AddListener(action);
    }

    void OnToggleChanged(bool isOn) => RefreshSprite(isOn);

    void RefreshSprite(bool isOn)
    {
        if (!switchImage) return;
        switchImage.sprite = isOn ? spriteOn : spriteOff;
    }
}
