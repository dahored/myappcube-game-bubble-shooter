using UnityEditor;
using UnityEngine;

// Los bloques del catálogo que se encienden con un interruptor: apagados ocupan una línea y al
// marcarlos se abren solos.
//
// Van así y no escondidos del todo porque un bloque invisible no se puede activar nunca. La línea
// con su nombre y su casilla es lo mínimo para que exista, y es lo que hace que un poder que no
// usa ninguno de los tres se lea de un vistazo en vez de en cuarenta renglones.
[CustomPropertyDrawer(typeof(Lightning.Settings))]
[CustomPropertyDrawer(typeof(SpecialBubbleSkin.Spark))]
public class ToggledBlockDrawer : PropertyDrawer
{
    const string FLAG = "enabled";
    const float  GAP  = 2f;

    public override void OnGUI(Rect area, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(area, label, property);

        var flag = property.FindPropertyRelative(FLAG);
        var line = new Rect(area.x, area.y, area.width, EditorGUIUtility.singleLineHeight);

        // La casilla va en el sitio del VALOR y no pegada al nombre: así queda en la misma
        // columna que el resto de los campos del Inspector en vez de flotando a media altura.
        EditorGUI.LabelField(line, label);

        var box = new Rect(line.x + EditorGUIUtility.labelWidth, line.y, line.width - EditorGUIUtility.labelWidth, line.height);
        flag.boolValue = EditorGUI.ToggleLeft(box, flag.boolValue ? "activo" : "apagado", flag.boolValue);

        if (flag.boolValue)
        {
            EditorGUI.indentLevel++;
            float y = line.yMax + GAP;

            foreach (var child in Children(property))
            {
                float h = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(new Rect(area.x, y, area.width, h), child, true);
                y += h + GAP;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float h = EditorGUIUtility.singleLineHeight;

        if (!property.FindPropertyRelative(FLAG).boolValue) return h;

        foreach (var child in Children(property)) h += EditorGUI.GetPropertyHeight(child, true) + GAP;

        return h;
    }

    // Todos los campos del bloque menos el propio interruptor, que ya se dibujó en la cabecera.
    static System.Collections.Generic.IEnumerable<SerializedProperty> Children(SerializedProperty property)
    {
        var walker = property.Copy();
        var end    = property.GetEndProperty();

        if (!walker.NextVisible(enterChildren: true)) yield break;

        do
        {
            if (SerializedProperty.EqualContents(walker, end)) yield break;
            if (walker.name == FLAG) continue;

            yield return walker.Copy();
        }
        while (walker.NextVisible(enterChildren: false));
    }
}

// La órbita no tiene interruptor: lo que la enciende es tener sprites que girar. Sin ninguno, el
// resto de los ajustes no tienen a qué aplicarse y estorban.
[CustomPropertyDrawer(typeof(SpecialBubbleSkin.Orbit))]
public class OrbitDrawer : PropertyDrawer
{
    const float GAP = 2f;

    static readonly string[] REST = { "radius", "size", "speed" };

    public override void OnGUI(Rect area, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(area, label, property);

        var sprites = property.FindPropertyRelative("sprites");

        float h = EditorGUI.GetPropertyHeight(sprites, true);
        EditorGUI.PropertyField(new Rect(area.x, area.y, area.width, h), sprites, label, true);

        float y = area.y + h + GAP;

        if (sprites.arraySize > 0)
        {
            EditorGUI.indentLevel++;

            foreach (string field in REST)
            {
                var child = property.FindPropertyRelative(field);
                float ch  = EditorGUI.GetPropertyHeight(child, true);

                EditorGUI.PropertyField(new Rect(area.x, y, area.width, ch), child, true);
                y += ch + GAP;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var sprites = property.FindPropertyRelative("sprites");
        float h = EditorGUI.GetPropertyHeight(sprites, true) + GAP;

        if (sprites.arraySize == 0) return h;

        foreach (string field in REST)
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true) + GAP;

        return h;
    }
}
