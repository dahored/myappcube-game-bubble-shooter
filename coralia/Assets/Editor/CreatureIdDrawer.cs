using UnityEditor;
using UnityEngine;

// Dibuja un campo marcado con [CreatureId] como un desplegable con lo que haya en el
// CreatureCatalog, separado en criaturas y objetos.
//
// Existe porque escribirlo a mano no falla al guardar: falla en partida, con la criatura saliendo
// genérica y sin que nada diga por qué. Un dedazo en "coqui" es invisible hasta que alguien juega
// ese nivel.
//
// Lo que ya esté escrito y no exista en el catálogo NO se borra: se enseña marcado para que se
// vea que sobra, pero tirarlo convertiría un id que quizá solo falta por crear en una pérdida
// silenciosa.
[CustomPropertyDrawer(typeof(CreatureIdAttribute))]
public class CreatureIdDrawer : PropertyDrawer
{
    const string NONE = "(ninguno)";

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        var catalog = CreatureCatalog.Instance;

        if (catalog == null)
        {
            // Sin catálogo se deja el campo de texto: mejor poder escribirlo que quedarse sin
            // forma de rellenarlo hasta que el asset exista.
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        position = EditorGUI.PrefixLabel(position, label);

        string current = property.stringValue;
        bool   known   = string.IsNullOrEmpty(current) || catalog.EntryFor(current) != null;
        string shown   = string.IsNullOrEmpty(current) ? NONE : known ? current : $"{current}  ⚠ no está en el catálogo";

        if (!EditorGUI.DropdownButton(position, new GUIContent(shown), FocusType.Keyboard)) return;

        var menu = new GenericMenu();

        menu.AddItem(new GUIContent(NONE), string.IsNullOrEmpty(current), () => Set(property, ""));
        menu.AddSeparator("");

        Add(menu, property, "Criaturas", catalog.CreatureIds, current);
        Add(menu, property, "Objetos",   catalog.ObjectIds,   current);

        menu.DropDown(position);
    }

    static void Add(GenericMenu menu, SerializedProperty property, string group, string[] ids, string current)
    {
        if (ids == null || ids.Length == 0)
        {
            menu.AddDisabledItem(new GUIContent($"{group}/(vacío)"));
            return;
        }

        foreach (string id in ids)
            menu.AddItem(new GUIContent($"{group}/{id}"), id == current, () => Set(property, id));
    }

    // Se captura el SerializedObject y se aplica dentro del callback: el menú se cierra en otro
    // frame, y para entonces el drawer ya no está dibujando — tocar la propiedad sin aplicar
    // dejaría el cambio sin guardar.
    static void Set(SerializedProperty property, string value)
    {
        property.stringValue = value;
        property.serializedObject.ApplyModifiedProperties();
    }
}
