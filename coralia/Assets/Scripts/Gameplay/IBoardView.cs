using System.Collections.Generic;
using UnityEngine;

// Lo que un poder necesita saber del tablero para decidir a qué le da.
//
// Es una interfaz y no 'GridController' a secas porque quien pregunta no siempre es una partida:
// la demostración de un tutorial dibuja su propio racimo y tiene que poder responder lo mismo. Si
// BoosterRules dependiera del grid de verdad, un tutorial no podría enseñar un poder sin montar
// medio juego detrás.
//
// Y es MÍNIMA a propósito: dos miembros. Todo lo que se le añada aquí es trabajo que cualquier
// demostración futura va a tener que implementar para enseñar un poder de una línea.
public interface IBoardView
{
    IEnumerable<Vector2Int> Occupied { get; }

    // False si esa celda está vacía. Con una sola pregunta se resuelven las dos cosas que hacen
    // falta —si hay algo y de qué color es— en vez de obligar a preguntar dos veces por celda.
    bool TryGetColor(Vector2Int cell, out BubbleColor color);
}
