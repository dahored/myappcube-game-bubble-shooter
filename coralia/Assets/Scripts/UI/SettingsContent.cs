using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Solo.MOST_IN_ONE;
using System.Linq;

public class SettingsContent : MonoBehaviour
{
    [Header("Audio — Preferencias")]
    [SerializeField] Slider gameSlider;
    [SerializeField] Slider uiSlider;
    [SerializeField] Slider popSlider;
    [SerializeField] GameObject musicSlider;
    [SerializeField] GameObject soundSlider;

    [Header("Toggles")]
    [SerializeField] SettingsToggle musicToggle;
    [SerializeField] SettingsToggle soundToggle;
    [SerializeField] SettingsToggle vibrationToggle;

    [Header("Language")]
    [SerializeField] TMP_Dropdown languageDropdown;

    [Header("Account & Support")]
    [SerializeField] Button profileButton;
    [SerializeField] Button subscriptionButton;
    [SerializeField] Button howToPlayButton;
    [SerializeField] Button helpButton;
    [SerializeField] Button restoreButton;

    [Header("Community")]
    [SerializeField] Button rateButton;
    [SerializeField] Button shareButton;
    [SerializeField] Button socialButton;
    [SerializeField] Button websiteButton;

    [Header("Legal")]
    [SerializeField] Button privacyButton;
    [SerializeField] Button termsButton;

    [Header("URLs")]
    [SerializeField] string privacyUrl;
    [SerializeField] string termsUrl;
    [SerializeField] string supportUrl;
    [SerializeField] string socialUrl;
    [SerializeField] string websiteUrl;

    static readonly (string code, string name)[] Languages = {
        ("es", "Español"),
        ("en", "English"),
        ("it", "Italiano"),
        ("fr", "Français"),
        ("de", "Deutsch"),
        ("pt", "Português"),
    };

    void Awake()
    {
        SetupSliders();
        SetupToggles();
        SetupLanguage();
        SetupButtons();
    }

    void SetupSliders()
    {
        if (gameSlider)
        {
            gameSlider.value = SaveManager.MusicVolume;
            gameSlider.onValueChanged.AddListener(v => AudioManager.Instance?.SetMusicVolume(v));
        }
        if (uiSlider)
        {
            uiSlider.value = SaveManager.UiVolume;
            uiSlider.onValueChanged.AddListener(v => AudioManager.Instance?.SetUiVolume(v));
        }
        if (popSlider)
        {
            popSlider.value = SaveManager.PopVolume;
            popSlider.onValueChanged.AddListener(v => AudioManager.Instance?.SetPopVolume(v));
        }
    }

    void SetupToggles()
    {
        if (musicToggle)
        {
            musicToggle.Value = SaveManager.MusicEnabled;
            if (musicSlider) musicSlider.SetActive(SaveManager.MusicEnabled);
            musicToggle.AddListener(v =>
            {
                SaveManager.MusicEnabled = v;
                AudioManager.Instance?.SetMusicEnabled(v);
                if (musicSlider) musicSlider.SetActive(v);
            });
        }
        if (soundToggle)
        {
            soundToggle.Value = SaveManager.SoundEnabled;
            if (soundSlider) soundSlider.SetActive(SaveManager.SoundEnabled);
            soundToggle.AddListener(v =>
            {
                SaveManager.SoundEnabled = v;
                AudioManager.Instance?.SetSoundEnabled(v);
                if (soundSlider) soundSlider.SetActive(v);
            });
        }
        if (vibrationToggle)
        {
            vibrationToggle.Value = SaveManager.Vibration;
            vibrationToggle.AddListener(v =>
            {
                SaveManager.Vibration = v;
                MOST_HapticFeedback.HapticsEnabled = v;
            });
        }
    }

    void SetupLanguage()
    {
        if (!languageDropdown) return;
        languageDropdown.AddOptions(Languages.Select(l => l.name).ToList());
        int current = System.Array.FindIndex(Languages, l => l.code == SaveManager.Language);

        languageDropdown.value = current < 0 ? 1 : current;
        languageDropdown.onValueChanged.AddListener(i =>
        {
            SaveManager.Language = Languages[i].code;
            LocaleManager.Reload();
        });
    }

    void SetupButtons()
    {
        Wire(helpButton,         () => OpenUrl(supportUrl));
        Wire(restoreButton,      OnRestorePurchases);
        Wire(rateButton,         OnRateApp);
        Wire(shareButton,        OnShare);
        Wire(socialButton,       () => OpenUrl(socialUrl));
        Wire(websiteButton,      () => OpenUrl(websiteUrl));
        Wire(privacyButton,      () => OpenUrl(privacyUrl));
        Wire(termsButton,        () => OpenUrl(termsUrl));
        Wire(profileButton,      () => { });
        Wire(subscriptionButton, () => { });
        Wire(howToPlayButton,    () => { });
    }

    static void Wire(Button btn, System.Action action)
    {
        if (btn) btn.onClick.AddListener(() => action());
    }

    static void OpenUrl(string url)
    {
        if (!string.IsNullOrEmpty(url)) Application.OpenURL(url);
    }

    static void OnRestorePurchases()
    {
        // Wire to IAP SDK once integrated (RevenueCat / Unity IAP)
        Debug.Log("[Settings] RestorePurchases — IAP SDK not yet integrated");
    }

    static void OnRateApp()
    {
#if UNITY_IOS
        UnityEngine.iOS.Device.RequestStoreReview();
#elif UNITY_ANDROID
        Application.OpenURL("market://details?id=" + Application.identifier);
#endif
    }

    static void OnShare()
    {
        // Wire to NativeShare plugin once added; copy to clipboard as fallback
        GUIUtility.systemCopyBuffer = Application.productName;
        Debug.Log("[Settings] Share — NativeShare plugin not yet integrated");
    }
}
