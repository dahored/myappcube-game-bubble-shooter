using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelMapController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform           contentRoot;
    [SerializeField] GameObject          levelNodePrefab;
    [SerializeField] GameObject          pearlPrefab;
    [SerializeField] GameObject          playerCardPrefab;  // card de usuario junto al nodo actual
    [SerializeField] bool                showPlayerNode;    // mostrar la card del usuario en el mapa
    [SerializeField] ScrollPinController scrollPinTop;      // pin arriba — nodo está debajo del viewport
    [SerializeField] ScrollPinController scrollPinBottom;   // pin abajo — nodo está arriba del viewport
    [SerializeField] OutOfLivesPanel     outOfLivesPanel;   // se muestra si SaveManager.Lives llega a 0 (issue #52)
    [SerializeField] StartGamePanel      startGamePanel;    // objetivo + boosters antes de entrar a Gameplay (issue #12)

    public const float NODE_SPACING =  300f; // distancia vertical entre nodos
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
        // Por si se entra a esta escena directo (sin pasar por Splash, donde se activa
        // normalmente) — así las transiciones animadas funcionan igual al testear.
        SceneTransition.Enabled = true;

        AudioManager.Instance?.PlayLobbyMusic();
        // Para test descomenta
        // SaveManager.MaxUnlockedLevel = 10;
        BuildMap();

        // Cubre "quitar/perder la última vida y aterrizar acá" — se muestra apenas carga
        // el mapa, pero SOLO como aviso reactivo de la bandera que arma SaveManager.LoseLife()
        // al momento justo de gastar la última vida. Ojo: NO chequea "Lives<=0" directo —
        // eso dispararía el panel también al navegar Home → LevelMap con 0 vidas de antes,
        // que es exactamente el caso que Diego pidió evitar.
        if (SaveManager.NotifyOutOfLivesOnMapLoad)
        {
            SaveManager.NotifyOutOfLivesOnMapLoad = false; // se consume una sola vez
            ShowOutOfLives();
        }
    }

    void ShowOutOfLives()
    {
        if (outOfLivesPanel != null) outOfLivesPanel.Open();
        else Debug.LogWarning("[LevelMapController] Falta asignar 'Out Of Lives Panel' en el Inspector.");
    }

    void BuildMap()
    {
        var levels = LoadAllLevels();
        levels.Sort((a, b) => a.id.CompareTo(b.id));
        int maxUnlocked = SaveManager.MaxUnlockedLevel;

        // Si GameplayController acaba de avanzar MaxUnlockedLevel de verdad (issue #55), este
        // nivel es el que hay que animar completándose — arranca mostrado como "Available"
        // (visual temporal, ver más abajo) y se revela a su estado real después de armar todo
        // el mapa, junto con el personaje deslizándose hasta el nuevo nodo actual.
        int justAdvancedFrom     = SaveManager.ConsumeJustAdvancedFromLevel();
        LevelNodeView justCompletedNode  = null;
        int           justCompletedStars = 0;
        bool          justCompletedGold  = false;
        Vector2       completionFromCardPos = default;
        RectTransform playerCardRT          = null;
        Vector2       playerCardToPos       = default;

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
            var nodeView = go.GetComponent<LevelNodeView>();
            nodeView.Setup(lvl.id, state, SaveManager.GetLevelStars(lvl.id));
            nodeView.OnClicked += OnLevelSelected;

            if (lvl.id == justAdvancedFrom)
            {
                justCompletedNode     = nodeView;
                justCompletedStars    = SaveManager.GetLevelStars(lvl.id);
                justCompletedGold     = state == NodeState.CompleteFirstTry;
                completionFromCardPos = CardOffsetPosition(positions[i]);
                // Invisible hasta que PlayCompletionSequence() lo revele con el pop.
                nodeView.HideForReveal();
            }

            // ScrollPins: inicializar con el RectTransform del nodo actual
            if (state == NodeState.Available)
                StartCoroutine(InitScrollPins(go.GetComponent<RectTransform>()));

            // Card de usuario junto al nodo actual
            if (showPlayerNode && state == NodeState.Available && playerCardPrefab != null)
            {
                playerCardToPos = CardOffsetPosition(positions[i]);
                var card   = Instantiate(playerCardPrefab, contentRoot);
                var cardRT = card.GetComponent<RectTransform>();
                cardRT.anchorMin = cardRT.anchorMax = new Vector2(0f, 0f);
                cardRT.pivot     = new Vector2(0.5f, 0.5f);
                cardRT.anchoredPosition = justAdvancedFrom > 0 ? completionFromCardPos : playerCardToPos;
                playerCardRT = cardRT;
            }
        }

        if (justCompletedNode != null && playerCardRT != null)
            StartCoroutine(PlayCompletionSequence(justCompletedNode, justCompletedStars, justCompletedGold,
                                                   playerCardRT, completionFromCardPos, playerCardToPos));
    }

    // Misma cuenta que ya hace el bloque de "Card de usuario junto al nodo actual" — factoreada
    // para poder calcular también la posición del nodo recién completado (issue #55).
    static Vector2 CardOffsetPosition(Vector2 nodePos)
    {
        bool isLeft = nodePos.x < 500f; // nodo en col izquierda
        float cardX = isLeft
            ? nodePos.x + CARD_SIDE_OFFSET  // nodo izq → card a la derecha
            : nodePos.x - CARD_SIDE_OFFSET; // nodo der → card a la izquierda
        return new Vector2(cardX, nodePos.y);
    }

    // Retorno de Gameplay tras un avance real (issue #55): deja que el mapa asiente, revela
    // el nodo recién completado, y desliza el PlayerCard por el mismo camino de perlas
    // (CubicBezier) hasta el nuevo nodo actual. completionFromCardPos/playerCardToPos ya
    // vienen calculadas por CardOffsetPosition() en BuildMap().
    IEnumerator PlayCompletionSequence(LevelNodeView node, int stars, bool gold,
                                        RectTransform card, Vector2 from, Vector2 to)
    {
        yield return new WaitForSeconds(0.4f);
        yield return node.PlayCompletionTransition(stars, gold);
        yield return MoveCardAlongPath(card, from, to);
    }

    IEnumerator MoveCardAlongPath(RectTransform card, Vector2 from, Vector2 to, float duration = 0.6f)
    {
        float   tangent = NODE_SPACING * PEARL_TANGENT;
        Vector2 p1      = from + new Vector2(0,  tangent);
        Vector2 p2      = to   + new Vector2(0, -tangent);

        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            card.anchoredPosition = CubicBezier(from, p1, p2, to, Mathf.Clamp01(t));
            yield return null;
        }
        card.anchoredPosition = to;
    }

    IEnumerator InitScrollPins(RectTransform nodeRT)
    {
        yield return null; // espera un frame para que el viewport tenga su rect calculado
        scrollPinBottom?.Init(nodeRT, NODE_SPACING);
        scrollPinTop?.Init(nodeRT, NODE_SPACING);
    }

    NodeState GetState(int levelId, int maxUnlocked)
    {
        if (levelId > maxUnlocked)  return NodeState.Locked;
        if (levelId == maxUnlocked) return NodeState.Available;
        return SaveManager.IsLevelGold(levelId) ? NodeState.CompleteFirstTry : NodeState.Completed;
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
        if (SaveManager.Lives <= 0) { ShowOutOfLives(); return; }

        var level = LevelLoader.LoadById(levelId);
        if (level == null)
        {
            Debug.LogError($"[LevelMapController] No se encontró el nivel {levelId}");
            return;
        }

        // StartGamePanel puede no estar asignado todavía (WIP) — en ese caso se navega
        // directo, como antes, para no dejar el mapa sin poder jugar.
        if (startGamePanel != null)
        {
            startGamePanel.Show(level);
        }
        else
        {
            Debug.LogWarning("[LevelMapController] Falta asignar 'Start Game Panel' en el Inspector — se navega directo a Gameplay.");
            PlayerPrefs.SetInt("selected_level", levelId);
            SceneLoader.GoTo(SceneLoader.GAMEPLAY);
        }
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
