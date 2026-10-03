using UnityEditor;
using UnityEngine;

// Muestra en el Inspector lo que de verdad se va a cobrar por un pack de booster.
//
// Hace falta porque el precio final es una propiedad calculada y Unity solo dibuja campos
// serializados: sin esto hay que hacer la resta de cabeza para saber si un 20% sobre 50 deja el
// pack en 40 o en 41, que es justo donde se cuelan los errores de precio.
[CustomPropertyDrawer(typeof(BoosterCatalog.Pack))]
public class BoosterPackDrawer : PropertyDrawer
{
    const int LINES = 5;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        EditorGUIUtility.singleLineHeight * LINES + EditorGUIUtility.standardVerticalSpacing * (LINES + 1);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var amount   = property.FindPropertyRelative("amount");
        var price    = property.FindPropertyRelative("price");
        var discount = property.FindPropertyRelative("discountPercent");

        var row = new Rect(position.x, position.y + EditorGUIUtility.standardVerticalSpacing,
                           position.width, EditorGUIUtility.singleLineHeight);

        EditorGUI.LabelField(row, label, EditorStyles.boldLabel);
        Next(ref row);

        EditorGUI.indentLevel++;

        EditorGUI.PropertyField(row, amount);   Next(ref row);
        EditorGUI.PropertyField(row, price);    Next(ref row);
        EditorGUI.PropertyField(row, discount); Next(ref row);

        // Deshabilitado y no un LabelField suelto: así se alinea con los campos de arriba y se lee
        // como lo que es, un valor del pack, aunque no se pueda tocar.
        using (new EditorGUI.DisabledScope(true))
            EditorGUI.IntField(row, "Se cobra", BoosterCatalog.Pack.Discounted(price.intValue, discount.intValue));

        EditorGUI.indentLevel--;

        EditorGUI.EndProperty();
    }

    static void Next(ref Rect row) =>
        row.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
}
