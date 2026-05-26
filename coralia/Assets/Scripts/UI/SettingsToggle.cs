using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsToggle : MonoBehaviour
{
    [SerializeField] TMP_Text labelText;
    [SerializeField] Toggle   toggle;

    public string Label
    {
        get => labelText ? labelText.text : "";
        set { if (labelText) labelText.text = value; }
    }

    public bool Value
    {
        get => toggle && toggle.isOn;
        set { if (toggle) toggle.SetIsOnWithoutNotify(value); }
    }

    public void AddListener(UnityEngine.Events.UnityAction<bool> action)
    {
        if (toggle) toggle.onValueChanged.AddListener(action);
    }
}
