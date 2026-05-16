using System.Collections;
using UnityEngine;

public class FloatAnimation : MonoBehaviour
{
    [SerializeField] float amplitude  = 12f;   // altura del movimiento en px
    [SerializeField] float frequency  = 0.6f;  // velocidad del ciclo
    [SerializeField] float phaseOffset = 0f;   // desfase para que no floten igual
    [SerializeField] float startDelay = 0f;    // espera antes de empezar

    RectTransform _rt;
    Vector2 _origin;
    bool _active;
    float _startTime;

    void Awake() => _rt = GetComponent<RectTransform>();

    void Start() => StartCoroutine(DelayedStart());

    IEnumerator DelayedStart()
    {
        yield return new WaitForSeconds(startDelay);
        var anim = GetComponent<Animator>();
        _origin = _rt.anchoredPosition;
        _startTime = Time.time;
        _active = true;
    }

    void Update()
    {
        if (!_active) return;
        float elapsed = Time.time - _startTime;
        float ramp = Mathf.Clamp01(elapsed / 0.4f);
        float y = Mathf.Sin((elapsed * frequency + phaseOffset) * Mathf.PI * 2f) * amplitude * ramp;
        _rt.anchoredPosition = _origin + new Vector2(0, y);
    }
}
