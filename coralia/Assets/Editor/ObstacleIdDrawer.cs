using UnityEditor;
using UnityEngine;

// Dibuja un campo marcado con [ObstacleId] como un desplegable con los materiales del
// ObstacleCatalog.
//
// Mismo criterio que RescuableIdDrawer: lo que ya esté escrito y no exista NO se borra, se
// enseña marcado. Tirarlo convertiría un material que quizá solo falta por crear en una pérdida
// silenciosa.
[CustomPropertyDrawer(typeof(ObstacleIdAttribute))]
public class ObstacleIdDrawer : PropertyDrawer
{
    const string NONE = "(sin caparazón)";

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        string[] ids = ObstacleCatalog.All;

        if (ids.Length == 0)
        {
            // Sin catálogo se deja el campo de texto: mejor poder escribirlo que quedarse sin
            // forma de rellenarlo hasta que el asset exista.
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        position = EditorGUI.PrefixLabel(position, label);

        string current = property.stringValue;
        bool   known   = string.IsNullOrEmpty(current) || ObstacleCatalog.Exists(current);
        string shown   = string.IsNullOrEmpty(current) ? NONE : known ? current : $"{current}  ⚠ no está en el ObstacleCatalog";

        if (!EditorGUI.DropdownButton(position, new GUIContent(shown), FocusType.Keyboard)) return;

        var menu = new GenericMenu();

        menu.AddItem(new GUIContent(NONE), string.IsNullOrEmpty(current), () => Set(property, ""));
        menu.AddSeparator("");

        foreach (string id in ids)
            menu.AddItem(new GUIContent(id), id == current, () => Set(property, id));

        menu.DropDown(position);
    }

    // Se aplica dentro del callback: el menú se cierra en otro frame, y para entonces el drawer
    // ya no está dibujando — tocar la propiedad sin aplicar dejaría el cambio sin guardar.
    static void Set(SerializedProperty property, string value)
    {
        property.stringValue = value;
        property.serializedObject.ApplyModifiedProperties();
    }
}
