// Lo implementa todo lo que se construye a partir de WorldMapDefinition: el suelo, el camino y
// lo que venga.
//
// Hace falta porque los valores del mundo viven en la definición pero las mallas las arman los
// hijos: al mover el zigzag en el Inspector, OnValidate salta en la definición, no en ellos. Sin
// este aviso no se repinta nada hasta tocar algo del suelo o del camino a mano.
public interface IWorldMapRebuildable
{
    void Rebuild();
}
