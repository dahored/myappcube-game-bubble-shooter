using System;
using UnityEngine;

// Los 14 colores de burbuja del juego, más el comodín. Rainbow va último a propósito: no es un
// color más de la paleta, es la regla de match (ver LinksWith). Los colores nuevos entran antes.
//
// Un nivel no usa los 14: elige un puñado en available_colors. El GDD §2.4 tope la dificultad en
// 6 colores simultáneos — esta lista es la paleta disponible, no lo que se pone en un nivel.
public enum BubbleColor
{
    Red, Blue, Yellow, Green, Purple, Orange,
    Pink, Grey, Brown, Black, RedWine, MintGreen, DarkBlue, DarkGrey,
    Rainbow,
}

public static class BubbleColorExtensions
{
    public static BubbleColor Parse(string value) => value switch
    {
        "red"        => BubbleColor.Red,
        "blue"       => BubbleColor.Blue,
        "yellow"     => BubbleColor.Yellow,
        "green"      => BubbleColor.Green,
        "purple"     => BubbleColor.Purple,
        "orange"     => BubbleColor.Orange,
        "pink"       => BubbleColor.Pink,
        "grey"       => BubbleColor.Grey,
        "brown"      => BubbleColor.Brown,
        "black"      => BubbleColor.Black,
        "red_wine"   => BubbleColor.RedWine,
        "mint_green" => BubbleColor.MintGreen,
        "dark_blue"  => BubbleColor.DarkBlue,
        "dark_grey"  => BubbleColor.DarkGrey,
        "rainbow"    => BubbleColor.Rainbow,
        _            => throw new ArgumentException($"Color de burbuja desconocido: {value}"),
    };

    // Regla de match del GDD 1.4: rainbow conecta con cualquier color, en cualquier dirección.
    public static bool LinksWith(this BubbleColor a, BubbleColor b) =>
        a == b || a == BubbleColor.Rainbow || b == BubbleColor.Rainbow;

    // De qué color es cada uno, para lo que hay que PINTAR de ese color sin ser una burbuja: el
    // líquido de la Tinta de Pulpo, la zona que marca su mira.
    //
    // Escrito y no sacado del sprite. Leer el píxel central parecía más limpio —una sola fuente,
    // el arte— pero no funciona: las texturas importadas no son legibles, GetPixel lanza, y todo
    // acababa en blanco (reportado por Diego).
    //
    // Tampoco es una copia del arte: es el color REPRESENTATIVO de cada uno, el que se usa para
    // teñir una capa que no es la burbuja. Que el sprite tenga brillos, sombras y degradados no
    // cambia cuál es "el rojo".
    public static Color Tint(this BubbleColor color) => color switch
    {
        BubbleColor.Red       => new Color(0.93f, 0.24f, 0.26f),
        BubbleColor.Blue      => new Color(0.26f, 0.60f, 0.95f),
        BubbleColor.Yellow    => new Color(0.98f, 0.80f, 0.22f),
        BubbleColor.Green     => new Color(0.36f, 0.80f, 0.36f),
        BubbleColor.Purple    => new Color(0.68f, 0.36f, 0.92f),
        BubbleColor.Orange    => new Color(0.98f, 0.58f, 0.20f),
        BubbleColor.Pink      => new Color(0.98f, 0.50f, 0.74f),
        BubbleColor.Grey      => new Color(0.68f, 0.70f, 0.73f),
        BubbleColor.Brown     => new Color(0.60f, 0.42f, 0.28f),
        BubbleColor.Black     => new Color(0.24f, 0.25f, 0.30f),
        BubbleColor.RedWine   => new Color(0.60f, 0.16f, 0.30f),
        BubbleColor.MintGreen => new Color(0.48f, 0.90f, 0.72f),
        BubbleColor.DarkBlue  => new Color(0.18f, 0.32f, 0.68f),
        BubbleColor.DarkGrey  => new Color(0.38f, 0.40f, 0.45f),

        // El comodín no tiene uno: es todos. Quien lo pinte tendrá que resolverlo aparte.
        _                     => Color.white,
    };
}
