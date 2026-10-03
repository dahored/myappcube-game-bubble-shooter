// Qué poder lleva una burbuja, aparte de su color. None es la burbuja normal de toda la vida.
//
// Va SEPARADO de BubbleColor a propósito: un poder no es un color más de la paleta. Metido en
// el enum de colores, cada cosa que recorre colores (RollColor, ColorsOnGrid, ReachableColorPool,
// LinksWith, el switch de sprites) tendría que acordarse de saltearlo, y la que se olvide sortea
// bombas gratis desde el cañón.
//
// Los próximos boosters del GDD §3.2 que también salen disparados entran acá: Rayo, Pez
// explorador. Los que no disparan nada (Mira láser, +1 disparo) no son burbujas y no van acá.
public enum BubbleSpecial
{
    None,
    Bomb,
}
