using UnityEngine;

// Vive en el prefab "ResourcePillLives Variant" — acá es donde vive la lógica específica
// de vidas (SaveManager.Lives/LoseLife/etc.), el ResourcePillView de abajo sigue siendo un
// componente tonto y reutilizable (ícono + texto + badge), sin saber nada de vidas.
//
// Se refresca 1 vez por segundo (no hace falta por frame, es un cronómetro de minutos) y
// también al habilitarse — así una instancia en una escena que recién carga (ej. volver al
// Level Map) muestra el estado correcto al toque, sin esperar el primer tick.
public class LivesPillView : MonoBehaviour
{
    [SerializeField] ResourcePillView pill;

    const float REFRESH_INTERVAL = 1f;
    float _timer;

    void Awake()
    {
        if (!pill) Debug.LogWarning("[LivesPillView] Falta asignar 'Pill' en el Inspector.");
    }

    void OnEnable()
    {
        _timer = 0f;
        Refresh();
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < REFRESH_INTERVAL) return;
        _timer = 0f;
        Refresh();
    }

    void Refresh()
    {
        if (!pill) return;

        if (SaveManager.IsInfiniteLivesActive)
        {
            pill.SetBadgeInfinite();
            pill.SetTimer(SaveManager.TimeUntilInfiniteLivesEnds());
            return;
        }

        int lives = SaveManager.Lives; // ya aplica la regeneración pendiente al leerlo
        pill.SetBadge(lives.ToString());

        if (lives >= SaveManager.MAX_LIVES) pill.SetFull();
        else                                pill.SetTimer(SaveManager.TimeUntilNextLife());
    }
}
