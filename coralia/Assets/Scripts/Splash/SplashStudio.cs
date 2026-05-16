using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class SplashStudio : MonoBehaviour
{
    const float TOTAL_DURATION = 2.7f;

    bool _done;

    void Start() => StartCoroutine(WaitAndGoNext());

    void Update()
    {
        if (_done) return;
        bool tapped = (Mouse.current    != null && Mouse.current.leftButton.wasReleasedThisFrame) ||
                      (Touchscreen.current != null && Touchscreen.current.primaryTouch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended);
        if (tapped) Skip();
    }

    IEnumerator WaitAndGoNext()
    {
        yield return new WaitForSeconds(TOTAL_DURATION);
        GoNext();
    }

    void Skip()
    {
        _done = true;
        StopAllCoroutines();
        GoNext();
    }

    void GoNext()
    {
        _done = true;
        SceneLoader.GoTo(SceneLoader.SPLASH_GAME);
    }
}
