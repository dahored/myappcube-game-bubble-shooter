using System.Collections.Generic;
using UnityEngine;

public class LevelMapController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform contentRoot;
    [SerializeField] GameObject levelNodePrefab;
    [SerializeField] GameObject pearlPrefab;
    [SerializeField] GameObject playerCardPrefab;  // card de usuario junto al nodo actual
    [SerializeField] bool       showPlayerNode;    // mostrar la card del usuario en el mapa

    const float NODE_SPACING    =  300f; // distancia vertical entre nodos
    const float PEARL_SPACING   =  100f; // distancia entre cada perla del path
    const float PEARL_SIZE      =   45f; // tamaño de cada perla (width y height)
    const float PEARL_TANGENT   = 0.85f; // tangent de curvatura de las perlas
    const float X_LEFT          =  400f; // columna izquierda del zigzag
    const float X_RIGHT         =  600f; // columna derecha del zigzag
    const float TOP_PADDING      =  400f; // espacio sobre el último nodo
    const float BOTTOM_PADDING   =  400f; // espacio bajo el primer nodo
    const float CARD_SIDE_OFFSET =  160f; // distancia horizontal de la card al nodo actual

    void Start()
    {
        AudioManager.Instance?.PlayLobbyMusic();
        BuildMap();
    }

    void BuildMap()
    {
        var levels = LoadAllLevels();
        levels.Sort((a, b) => a.id.CompareTo(b.id));
        int maxUnlocked = SaveManager.MaxUnlockedLevel;

        // Ajustar altura del Content (pivot bottom → Y positivo sube)
        var contentRT     = contentRoot.GetComponent<RectTransform>();
        float totalHeight = (levels.Count - 1) * NODE_SPACING + BOTTOM_PADDING + TOP_PADDING;
        contentRT.sizeDelta = new Vector2(contentRT.sizeDelta.x, totalHeight);

        // Pre-calcular posiciones: nivel 1 abajo (Y pequeño), último arriba (Y grande)
        var positions = new Vector2[levels.Count];
        for (int i = 0; i < levels.Count; i++)
        {
            float x = (i % 2 == 0) ? X_LEFT : X_RIGHT;
            float y = BOTTOM_PADDING + i * NODE_SPACING;
            positions[i] = new Vector2(x, y);
        }

        // Imagen transparente que cubre todo el Content para que el scroll reciba drags en zonas vacías
        var hitArea    = new GameObject("HitArea").AddComponent<UnityEngine.UI.Image>();
        hitArea.color  = Color.clear;
        hitArea.raycastTarget = true;
        hitArea.transform.SetParent(contentRoot, false);
        var hitRT      = hitArea.GetComponent<RectTransform>();
        hitRT.anchorMin = Vector2.zero;
        hitRT.anchorMax = Vector2.one;
        hitRT.offsetMin = hitRT.offsetMax = Vector2.zero;
        hitArea.transform.SetAsFirstSibling(); // detrás de todo

        // 1. Perlas primero → quedan debajo en la jerarquía → se dibujan detrás
        for (int i = 1; i < levels.Count; i++)
            DrawPearls(positions[i - 1], positions[i]);

        // 2. Nodos después → quedan encima en la jerarquía → se dibujan delante
        for (int i = 0; i < levels.Count; i++)
        {
            var lvl   = levels[i];
            var go    = Instantiate(levelNodePrefab, contentRoot);
            go.name   = $"Level_{lvl.id}";
            var state = GetState(lvl.id, maxUnlocked);
            go.GetComponent<RectTransform>().anchoredPosition = positions[i];
            go.GetComponent<LevelNodeView>().Setup(lvl.id, state, 0);

            // Card de usuario junto al nodo actual
            if (showPlayerNode && state == NodeState.Available && playerCardPrefab != null)
            {
                bool isLeft = positions[i].x < 500f; // nodo en col izquierda
                float cardX = isLeft
                    ? positions[i].x + CARD_SIDE_OFFSET  // nodo izq → card a la derecha
                    : positions[i].x - CARD_SIDE_OFFSET; // nodo der → card a la izquierda
                var card   = Instantiate(playerCardPrefab, contentRoot);
                var cardRT = card.GetComponent<RectTransform>();
                cardRT.anchorMin = cardRT.anchorMax = new Vector2(0f, 0f);
                cardRT.pivot     = new Vector2(0.5f, 0.5f);
                cardRT.anchoredPosition = new Vector2(cardX, positions[i].y);
            }
        }
    }

    NodeState GetState(int levelId, int maxUnlocked)
    {
        if (levelId > maxUnlocked)  return NodeState.Locked;
        if (levelId == maxUnlocked) return NodeState.Available;
        return NodeState.Completed; // TODO: NodeState.CompleteFirstTry desde SaveManager
    }

    void DrawPearls(Vector2 from, Vector2 to)
    {
        // Cubic Bezier con tangentes verticales:
        // P1 sale de "from" hacia arriba → P2 llega a "to" desde abajo
        // El giro lateral ocurre en el centro → crea S continua natural
        float   tangent = NODE_SPACING * PEARL_TANGENT;
        Vector2 p1      = from + new Vector2(0,  tangent);
        Vector2 p2      = to   + new Vector2(0, -tangent);

        float approxLen = Vector2.Distance(from, p1) + Vector2.Distance(p1, p2) + Vector2.Distance(p2, to);
        int   count     = Mathf.FloorToInt(approxLen / PEARL_SPACING);

        for (int i = 1; i < count; i++)
        {
            float   t     = (float)i / count;
            Vector2 pos   = CubicBezier(from, p1, p2, to, t);
            var     pearl = Instantiate(pearlPrefab, contentRoot);
            var     pearlRT = pearl.GetComponent<RectTransform>();
            pearlRT.anchoredPosition = pos;
            pearlRT.sizeDelta = new Vector2(PEARL_SIZE, PEARL_SIZE);
        }
    }

    // Curva Bezier cúbica: tangentes en P0→P1 y P2→P3 controlan entrada y salida
    static Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1f - t;
        return u*u*u*p0 + 3*u*u*t*p1 + 3*u*t*t*p2 + t*t*t*p3;
    }

    void OnLevelSelected(int levelId)
    {
        PlayerPrefs.SetInt("selected_level", levelId);
        SceneLoader.GoTo(SceneLoader.GAMEPLAY);
    }

    List<LevelData> LoadAllLevels()
    {
        var result = new List<LevelData>();
        var jsons = Resources.LoadAll<TextAsset>("Levels");
        foreach (var json in jsons)
        {
            var lvl = JsonUtility.FromJson<LevelData>(json.text);
            if (lvl != null) result.Add(lvl);
        }
        return result;
    }
}
