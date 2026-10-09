// Los poderes del juego (GDD §3.2). None es "ninguno".
//
// Va SEPARADO de BubbleColor a propósito: un poder no es un color más de la paleta. Metido en el
// enum de colores, cada cosa que recorre colores (RollColor, ColorsOnGrid, ReachableColorPool,
// LinksWith, el switch de sprites) tendría que acordarse de saltearlo, y la que se olvide sortea
// bombas gratis desde el cañón.
//
// Antes se llamaba BubbleSpecial, cuando el único que existía transformaba la burbuja del cañón.
// Son diez y solo cuatro hacen eso.
//
// Los valores NO se reordenan ni se agrupan por familia, por tentador que sea: Unity serializa un
// enum como su número, así que mover uno cambia en silencio a qué poder apunta cada entrada del
// catálogo y cada prefab ya configurado. Los nuevos se añaden AL FINAL, siempre.
//
// A qué familia pertenece cada uno lo dice BoosterCatalog, que es donde se puede cambiar sin
// romper nada.
public enum Booster
{
    None    = 0,
    Bomb    = 1,
    Rainbow = 2,
    Torpedo = 3,
}
