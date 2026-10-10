using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// El Inspector de una entrada del catálogo, enseñando solo lo que esa entrada usa.
//
// Qué se ve NO sale de una tabla "booster → campos". Esa tabla habría que acordarse de ampliarla
// con cada poder nuevo, nadie avisaría al olvidarlo, y el síntoma sería un campo invisible que
// alguien pasa media tarde buscando. Sale del PROPIO DATO: sin frames no hay nada que animar, con
// un solo frame no hay ritmo que poner, sin carga el adelanto del sonido no se aplica.
//
// Los bloques que se encienden con un interruptor —la órbita, el chisporroteo, la descarga— se
// pliegan a su línea cuando están apagados y se abren solos al marcarlos. Así no desaparecen (se
// podrían activar nunca) pero tampoco ocupan sitio en los nueve poderes que no los usan.
[CustomPropertyDrawer(typeof(BoosterCatalog.Entry))]
public class BoosterEntryDrawer : PropertyDrawer
{
    const float GAP  = 4f;
    const float HEAD = 6f;   // aire extra encima de cada encabezado

    // Un elemento de la lista: o una propiedad que dibujar, o el título de una sección.
    readonly struct Row
    {
        public readonly SerializedProperty prop;
        public readonly string title;

        public Row(SerializedProperty prop) { this.prop = prop; title = null; }
        public Row(string title)            { prop = null; this.title = title; }

        public bool IsHead => prop == null;
    }

    public override void OnGUI(Rect area, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(area, label, property);

        var booster = property.FindPropertyRelative("booster");
        string name = booster.enumValueIndex >= 0 && booster.enumValueIndex < booster.enumDisplayNames.Length
            ? booster.enumDisplayNames[booster.enumValueIndex]
            : "—";

        var line = new Rect(area.x, area.y, area.width, EditorGUIUtility.singleLineHeight);

        property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, name, toggleOnLabelClick: true);

        if (!property.isExpanded) { EditorGUI.EndProperty(); return; }

        EditorGUI.indentLevel++;
        float y = line.yMax + GAP;

        foreach (var row in Rows(property))
        {
            if (row.IsHead)
            {
                y += HEAD;
                var head = new Rect(area.x, y, area.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(head, row.title, EditorStyles.boldLabel);
                y = head.yMax + GAP;
                continue;
            }

            float h = EditorGUI.GetPropertyHeight(row.prop, true);
            EditorGUI.PropertyField(new Rect(area.x, y, area.width, h), row.prop, true);
            y += h + GAP;
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float h = EditorGUIUtility.singleLineHeight;

        if (!property.isExpanded) return h;

        h += GAP;

        foreach (var row in Rows(property))
            h += row.IsHead
                ? HEAD + EditorGUIUtility.singleLineHeight + GAP
                : EditorGUI.GetPropertyHeight(row.prop, true) + GAP;

        return h;
    }

    // La única lista, compartida por el dibujo y el alto. Separarlas sería tener dos veces las
    // mismas condiciones, y en cuanto una se quedara atrás los campos se pisarían entre sí.
    static IEnumerable<Row> Rows(SerializedProperty p)
    {
        SerializedProperty Get(string field) => p.FindPropertyRelative(field);

        var booster = p.FindPropertyRelative("booster");

        // De qué tira este poder, según BoosterRules. Ahí está porque es donde ya vive todo lo
        // que hay que escribir para uno nuevo; una tabla en el editor sería un sitio más que
        // recordar, y el que se olvidaría.
        var uses = BoosterRules.UsesOf((Booster)booster.intValue);

        yield return new Row(booster);
        yield return new Row(Get("family"));
        yield return new Row(Get("icon"));

        // --- La burbuja en el tablero ---
        //
        // No se filtra por familia aunque tiente: la Perla es de inicio y aun así pinta burbujas
        // en el tablero, porque lo que reparte son comodines. 'Family' dice cuándo se aplica el
        // poder, no si llega al tablero.
        var frames = Get("bubbleFrames");

        if (uses.HasFlag(BoosterRules.Uses.Bubble))
        {
            yield return new Row("La burbuja en el tablero");
            yield return new Row(frames);

            if (frames.arraySize > 0)
            {
                // Girar y pasar frames son excluyentes: con un solo dibujo el ritmo no pinta
                // nada y con varios el giro se lee como un error. Sale el que tiene sentido.
                if (frames.arraySize > 1) yield return new Row(Get("framesPerSecond"));
                else                      yield return new Row(Get("spin"));

                yield return new Row(Get("bubbleOrbit"));
                yield return new Row(Get("bubbleSpark"));
                yield return new Row(Get("glowOverride"));
            }
        }

        // --- Mientras está cargado ---
        if (uses.HasFlag(BoosterRules.Uses.Loaded))
        {
            var loop = Get("loadedLoopClip");

            yield return new Row("Mientras está cargado en el cañón");
            yield return new Row(loop);
            if (loop.objectReferenceValue != null) yield return new Row(Get("loadedLoopVolume"));
        }

        // --- Al estallar ---
        if (uses.HasFlag(BoosterRules.Uses.Burst))
        {
            var blast  = Get("burstBlast");
            var charge = blast.FindPropertyRelative("chargeTime");

            yield return new Row("Al estallar");
            yield return new Row(Get("burstClip"));
            yield return new Row(blast);

            // El adelanto solo existe mientras el poder carga: enseñarlo con la carga en cero es
            // ofrecer un ajuste que no hace nada.
            if (charge.floatValue > 0f)
            {
                yield return new Row(Get("chargeClip"));
                yield return new Row(Get("clipLead"));
            }

            yield return new Row(Get("burstSparkle"));
            yield return new Row(Get("burstPerBubble"));
            yield return new Row(Get("burstLightning"));
            yield return new Row(Get("vibrate"));
            yield return new Row(Get("shake"));
        }

        // --- Contagio ---
        if (uses.HasFlag(BoosterRules.Uses.Claim))
        {
            yield return new Row("Contagio: tiñe antes de estallar");
            yield return new Row(Get("claimStep"));
            yield return new Row(Get("claimFade"));
            yield return new Row(Get("claimHold"));
            yield return new Row(Get("claimMaxTotal"));
        }

        // --- Tienda ---
        yield return new Row("Panel de recarga");
        yield return new Row(Get("packs"));
    }
}
