using UnityEngine;

// Vive en el prefab "ResourcePillCoins Variant" — refresca el ValueText del ResourcePillView
// contra SaveManager.Coins. Mucho más simple que LivesPillView (sin badge ni cronómetro,
// monedas es solo un número), pero mismo patrón: 1 refresco por segundo + al habilitarse,
// para no depender de que cada lugar que gasta/gana monedas conozca esta instancia puntual.
public class CoinsPillView : MonoBehaviour
{
    [SerializeField] ResourcePillView pill;

    const float REFRESH_INTERVAL = 1f;
    float _timer;

    void Awake()
    {
        if (!pill) Debug.LogWarning("[CoinsPillView] Falta asignar 'Pill' en el Inspector.");
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
        if (pill) pill.SetValue(SaveManager.Coins);
    }
}
