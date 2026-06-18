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

    [Header("Sprites — nodo")]
    [SerializeField] Sprite spriteDefault;  // azul — bloqueado o completado con reintentos
    [SerializeField] Sprite spriteCurrent;  // morado — nivel disponible para jugar
    [SerializeField] Sprite spriteGold;     // dorado — completado en el primer intento


    // Configura el nodo con los datos del nivel
    public void Setup(int id, NodeState state, int starsEarned)
    {
        numberLabel.text = id.ToString();

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
    }
}