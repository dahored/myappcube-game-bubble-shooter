using System;

public enum BubbleColor { Red, Blue, Yellow, Green, Purple, Orange, Rainbow }

public static class BubbleColorExtensions
{
    public static BubbleColor Parse(string value) => value switch
    {
        "red"     => BubbleColor.Red,
        "blue"    => BubbleColor.Blue,
        "yellow"  => BubbleColor.Yellow,
        "green"   => BubbleColor.Green,
        "purple"  => BubbleColor.Purple,
        "orange"  => BubbleColor.Orange,
        "rainbow" => BubbleColor.Rainbow,
        _         => throw new ArgumentException($"Color de burbuja desconocido: {value}"),
    };

    // Regla de match del GDD 1.4: rainbow conecta con cualquier color, en cualquier dirección.
    public static bool LinksWith(this BubbleColor a, BubbleColor b) =>
        a == b || a == BubbleColor.Rainbow || b == BubbleColor.Rainbow;
}
