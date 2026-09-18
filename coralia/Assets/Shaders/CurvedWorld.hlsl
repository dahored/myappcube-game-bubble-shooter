#ifndef CORALIA_CURVED_WORLD_INCLUDED
#define CORALIA_CURVED_WORLD_INCLUDED

// La curvatura del mapa, en un solo lugar.
//
// El mundo es PLANO: el suelo, el camino, los nodos y las decoraciones viven en el plano XZ y el
// scroll los mueve en Z. Lo único curvo es cómo se dibujan — cada vértice se hunde en Y según lo
// lejos que esté de la cámara y lo corrido que esté del centro. El resultado se lee como un
// planeta que se aleja, sin que exista ninguna esfera.
//
// Va acá y no repetido en cada shader a propósito: si el suelo y las decoraciones calcularan la
// curva por separado, cualquier ajuste tendría que replicarse a mano y basta olvidarlo una vez
// para que los adornos floten por encima del terreno.
//
// Los parámetros son GLOBALES (los pone WorldMapCurve con Shader.SetGlobalFloat), así que no hay
// que asignarlos material por material ni mantenerlos sincronizados entre veinte prefabs.

float _CurveDepth;   // cuánto cae el terreno a medida que se aleja
float _CurveSide;    // cuánto caen los costados respecto del centro
float _CurveStart;   // hasta acá el terreno queda plano; la curva arranca después
float _CurveCenterX; // el eje sobre el que NO hay caída lateral

float3 CurveWorldPos(float3 positionWS)
{
    // Distancia hacia adelante desde donde empieza la curva. El tramo cercano se deja plano
    // porque es donde está el nodo actual: si se doblara ahí, el nodo bajo el dedo se movería
    // al scrollear y la mira se sentiría resbalosa.
    float depth = max(0.0, (positionWS.z - _WorldSpaceCameraPos.z) - _CurveStart);

    // Al cuadrado y no lineal: una caída lineal es una rampa recta, y lo que se busca es que el
    // horizonte se doble cada vez más rápido hacia el fondo.
    float side = positionWS.x - _CurveCenterX;

    positionWS.y -= _CurveDepth * depth * depth + _CurveSide * side * side;
    return positionWS;
}

#endif
