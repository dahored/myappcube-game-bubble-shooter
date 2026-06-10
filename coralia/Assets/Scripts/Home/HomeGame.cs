using UnityEngine;
using UnityEngine.UI;

public class HomeGame : MonoBehaviour
{
    [SerializeField] Button playButton;

    void Start()
    {
        if (playButton) playButton.onClick.AddListener(OnPlay);
        AudioManager.Instance?.PlayLobbyMusic();
    }

    void OnPlay() => SceneLoader.GoTo(SceneLoader.LEVEL_MAP);
}
