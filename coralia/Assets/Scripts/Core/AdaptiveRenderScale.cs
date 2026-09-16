using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Ajusta solo la resolución a la que se renderiza el juego, según lo que aguante el dispositivo.
//
// El mapa está limitado por fill rate (cuántos píxeles se pintan), no por cantidad de objetos ni
// por scripts. Con un valor fijo de Render Scale hay que elegir entre que se vea borroso en un
// teléfono potente o que vaya a saltos en uno viejo. Midiendo el tiempo de frame real se puede
// tener las dos cosas: arranca a resolución completa y baja solo en el hardware que no la banca.
//
// Nunca sube por las buenas: solo prueba subir un escalón después de un rato estable, y si al
// hacerlo empieza a perder frames, vuelve atrás y se queda ahí. Un ajuste que oscila se nota
// mucho más que uno que se queda un escalón por debajo del ideal.
//
// Se auto-instancia al arrancar el juego y sobrevive a los cambios de escena (mismo criterio que
// SceneTransition). La resolución es un ajuste global: si hubiera que agregarlo a mano en cada
// escena, tarde o temprano una quedaría sin él.
public class AdaptiveRenderScale : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateInstance()
    {
        var go = new GameObject("AdaptiveRenderScale");
        go.AddComponent<AdaptiveRenderScale>();
        DontDestroyOnLoad(go);
    }

    [Header("Objetivo")]
    [Tooltip("Cuadros por segundo a los que se apunta. 60 en móvil es lo esperable hoy.")]
    [SerializeField] int targetFrameRate = 60;
    [Tooltip("Fijar también Application.targetFrameRate. Sin esto, algunos dispositivos limitan a 30 por su cuenta.")]
    [SerializeField] bool applyTargetFrameRate = true;

    [Header("Límites")]
    [Tooltip("Resolución mínima a la que puede llegar. Por debajo de 0.5 se empieza a ver borroso de verdad.")]
    [Range(0.4f, 1f)]
    [SerializeField] float minScale = 0.55f;
    [Range(0.4f, 1f)]
    [SerializeField] float maxScale = 1f;
    [Tooltip("Cuánto cambia en cada ajuste.")]
    [SerializeField] float step = 0.05f;

    [Header("Medición")]
    [Tooltip("Cuántos segundos se promedia antes de decidir. Muy corto reacciona a tirones puntuales; muy largo tarda en acomodarse.")]
    [SerializeField] float sampleSeconds = 1.5f;
    [Tooltip("Qué porcentaje de frames puede pasarse del objetivo antes de bajar la resolución.")]
    [Range(0f, 0.5f)]
    [SerializeField] float allowedMissRatio = 0.1f;
    [Tooltip("Cuántos segundos seguidos sin perder un frame antes de animarse a subir un escalón.")]
    [SerializeField] float secondsBeforeProbingUp = 20f;

    UniversalRenderPipelineAsset _asset;
    float _originalScale;

    int   _frames;
    int   _missed;
    float _elapsed;
    float _stableTime;
    float _ceiling;       // hasta dónde se dejó de subir tras un intento fallido
    bool  _justProbedUp;

    void OnEnable()
    {
        _asset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset
              ?? GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;

        if (!_asset)
        {
            Debug.LogWarning("[AdaptiveRenderScale] No se encontró el asset de URP — el ajuste automático queda desactivado.", this);
            enabled = false;
            return;
        }

        // El asset es un ScriptableObject: lo que se le escriba en Play queda guardado en el
        // proyecto. Se anota el valor original para devolverlo al salir.
        _originalScale = _asset.renderScale;
        _ceiling       = maxScale;

        if (applyTargetFrameRate)
        {
            QualitySettings.vSyncCount   = 0;
            Application.targetFrameRate  = targetFrameRate;
        }

        Reset();
    }

    void OnDisable()
    {
        if (_asset) _asset.renderScale = _originalScale;
    }

    void Update()
    {
        // Sin escalar por Time.timeScale: interesa el tiempo real que tarda el dispositivo.
        float dt = Time.unscaledDeltaTime;
        float budget = 1f / Mathf.Max(targetFrameRate, 1);

        _frames++;
        _elapsed += dt;
        // Un 20% de margen: llegar a 16.6 ms clavados en cada frame no pasa nunca, y castigarlo
        // haría bajar la resolución sin motivo.
        if (dt > budget * 1.2f) _missed++;

        if (_elapsed < sampleSeconds) return;

        float missRatio = _frames > 0 ? (float)_missed / _frames : 0f;

        if (missRatio > allowedMissRatio)
        {
            // Si acabamos de probar subir y salió mal, ese nivel queda marcado como techo.
            if (_justProbedUp) _ceiling = Mathf.Max(minScale, _asset.renderScale - step);

            Apply(_asset.renderScale - step);
            _stableTime = 0f;
        }
        else
        {
            _stableTime += _elapsed;

            if (_stableTime >= secondsBeforeProbingUp && _asset.renderScale < _ceiling - 0.001f)
            {
                Apply(_asset.renderScale + step);
                _justProbedUp = true;
                _stableTime   = 0f;
                Reset();
                return;
            }
        }

        _justProbedUp = false;
        Reset();
    }

    void Apply(float scale)
    {
        _asset.renderScale = Mathf.Clamp(scale, minScale, maxScale);
    }

    void Reset()
    {
        _frames  = 0;
        _missed  = 0;
        _elapsed = 0f;
    }
}
