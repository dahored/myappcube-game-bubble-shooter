using System;

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
}
