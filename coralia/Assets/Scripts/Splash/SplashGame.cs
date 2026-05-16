using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class SplashGame : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI versionLabel;
    [SerializeField] TextMeshProUGUI loadingLabel;
    [SerializeField] TextMeshProUGUI percentLabel;
    [SerializeField] Image fillImage;

    const float MIN_SHOW   = 2.0f;
    const float DOT_PERIOD = 0.5f;

    void Start()
    {
        versionLabel.text = $"v{Application.version}";
        StartCoroutine(AnimateDots());
        StartCoroutine(LoadNextScene());
    }

    IEnumerator LoadNextScene()
    {
        var op = SceneManager.LoadSceneAsync(SceneLoader.LEVEL_MAP);
        op.allowSceneActivation = false;

        float elapsed = 0f;

        while (elapsed < MIN_SHOW || op.progress < 0.9f)
        {
            elapsed += Time.deltaTime;
            float loadProgress = Mathf.Clamp01(op.progress / 0.9f);
            float timeProgress = Mathf.Clamp01(elapsed / MIN_SHOW);
            float display = Mathf.Min(loadProgress, timeProgress);

            fillImage.fillAmount = display;
            if (percentLabel) percentLabel.text = $"{Mathf.RoundToInt(display * 100)}%";
            yield return null;
        }

        fillImage.fillAmount = 1f;
        if (percentLabel) percentLabel.text = "100%";
        yield return new WaitForSeconds(0.2f);
        SceneTransition.FadeOutThen(() => op.allowSceneActivation = true);
    }

    IEnumerator AnimateDots()
    {
        int dots = 0;
        while (true)
        {
            yield return new WaitForSeconds(DOT_PERIOD);
            dots = (dots % 3) + 1;
            loadingLabel.text = LocaleManager.Get("loading") + new string('.', dots);
        }
    }
}
