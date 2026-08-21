using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Estados posibles de un nodo en el mapa de niveles
public enum NodeState { Locked, Available, Completed, CompleteFirstTry }

// Controla la apariencia visual de un nodo en el mapa de niveles.
// Se llama desde LevelMapController pasando el id, estado y estrellas ganadas.
public class LevelNodeView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Image background;      // círculo de fondo del nodo
    [SerializeField] TMP_Text numberLabel;  // número del nivel
    [SerializeField] GameObject starRow;    // fila de estrellas (se oculta si no hay progreso)
    [SerializeField] Image[] stars;         // Star1, Star2, Star3 — tamaño fijo 3
    [SerializeField] GameObject lockIcon;   // candado (solo visible si bloqueado)
    [SerializeField] Button   button;       // clic/tap del nodo — dispara OnClicked si no está bloqueado

    public event System.Action<int> OnClicked;

    int       _id;
    NodeState _state;

    [Header("Sprites — nodo")]
    [SerializeField] Sprite spriteDefault;  // azul — bloqueado o completado con reintentos
    [SerializeField] Sprite spriteCurrent;  // morado — nivel disponible para jugar
    [SerializeField] Sprite spriteGold;     // dorado — completado en el primer intento

    [Header("Pulse — nodo actual")]
    [SerializeField] float pulseMin      = 1.00f;
    [SerializeField] float pulseMax      = 1.10f;
    [SerializeField] float pulseDuration = 0.25f;
    [SerializeField] float pulsePause    = 2.00f;
    [SerializeField] float pulseTotals   = 2.00f;

    [Header("Ripple — anillo expansivo")]
    [SerializeField] Image  ringImage      = null;  // Image hijo detrás del círculo
    [SerializeField] float  rippleScale    = 1.70f; // escala máxima del anillo
    [SerializeField] float  rippleAlpha    = 0.55f; // alpha inicial del anillo
    [SerializeField] float  rippleDuration = 0.45f; // duración de cada ripple



    // Configura el nodo con los datos del nivel
    public void Setup(int id, NodeState state, int starsEarned)
    {
        _id    = id;
        _state = state;
        numberLabel.text = id.ToString();

        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (_state != NodeState.Locked) OnClicked?.Invoke(_id);
            });
        }

        // Sprite del círculo según el estado del nivel
        switch (state)
        {
            case NodeState.Locked:           background.sprite = spriteDefault; break;
            case NodeState.Available:        background.sprite = spriteCurrent; break;
            case NodeState.Completed:        background.sprite = spriteDefault; break;
            case NodeState.CompleteFirstTry: background.sprite = spriteGold;    break;
        }

        // Mostrar starRow solo si hay progreso, activar solo las estrellas ganadas
        starRow.SetActive(starsEarned > 0);
        for (int i = 0; i < stars.Length; i++)
            stars[i].gameObject.SetActive(i < starsEarned);

        // Candado solo visible si el nivel está bloqueado
        lockIcon.SetActive(state == NodeState.Locked);

        // Pulso + ripple solo en el nodo disponible
        if (state == NodeState.Available)
        {
            if (ringImage) ringImage.gameObject.SetActive(true);
            StartCoroutine(PulseLoop());
        }
        else if (ringImage)
        {
            ringImage.gameObject.SetActive(false);
        }
    }

    IEnumerator PulseLoop()
    {
        while (true)
        {
            // 2 pulsos seguidos
            for (int p = 0; p < pulseTotals; p++)
            {
                if (ringImage) StartCoroutine(RippleOnce());
                for (float t = 0f; t < 1f; t += Time.deltaTime / pulseDuration)
                {
                    transform.localScale = Vector3.one * Mathf.Lerp(pulseMin, pulseMax, t);
                    yield return null;
                }
                for (float t = 0f; t < 1f; t += Time.deltaTime / pulseDuration)
                {
                    transform.localScale = Vector3.one * Mathf.Lerp(pulseMax, pulseMin, t);
                    yield return null;
                }
            }
            transform.localScale = Vector3.one * pulseMin;
            // Pausa
            yield return new WaitForSeconds(pulsePause);
        }
    }

    IEnumerator RippleOnce()
    {
        var c = ringImage.color;
        for (float t = 0f; t < 1f; t += Time.deltaTime / rippleDuration)
        {
            ringImage.transform.localScale = Vector3.one * Mathf.Lerp(1f, rippleScale, t);
            c.a = Mathf.Lerp(rippleAlpha, 0f, t);
            ringImage.color = c;
            yield return null;
        }
        // Reset para el próximo ripple
        ringImage.transform.localScale = Vector3.one;
        c.a = 0f;
        ringImage.color = c;
    }
}