Shader "Coralia/CurvedMapPreview"
{
    // Réplica del prototipo HTML (mapa-curvo-preview.html): el mapa de un capítulo es una
    // imagen plana larga (el "atlas"), y acá se remuestrea en dos pasadas matemáticas —
    // cilindro (compresión hacia el horizonte) + arco esférico (el horizonte se curva hacia
    // abajo en los bordes) — para lograr el look tipo Candy Crush. Sin nodos todavía (eso va
    // aparte, como pidió Diego) — esto es solo el "mundo".
    Properties
    {
        _MainTex   ("Atlas (mapa plano del capítulo)", 2D) = "white" {}
        _Scroll    ("Scroll (en 'alturas de pantalla' recorridas)", Float) = 0
        _MapScale  ("Alto total del atlas (AUTOMÁTICO — lo pisa CurvedMapNodePositioner al arrancar, no tocar a mano)", Float) = 10
        _ScrollMax ("Scroll máximo (lo calcula CurvedMapNodePositioner solo)", Float) = 0
        _Radius    ("Radio del cilindro (alturas de pantalla)", Float) = 0.7
        _HorizonY  ("Horizonte (0 = arriba de la pantalla, 1 = abajo)", Range(0,1)) = 0
        _FootY     ("Punto de apoyo / 'pie' (0 = arriba, 1 = abajo)", Range(0,1)) = 0.956
        _Drop      ("Profundidad del arco del horizonte", Range(0,0.5)) = 0.03
        _XStretch  ("Ensanche horizontal cerca del pie", Range(0,1)) = 0.208
        [Toggle] _RepeatV  ("Repetir la textura verticalmente en vez de estirarla (para texturas tileables como agua genérica; dejar en 0 para arte único de un capítulo)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Scroll, _MapScale, _ScrollMax, _Radius, _HorizonY, _FootY, _Drop, _XStretch, _RepeatV;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos : SV_POSITION;  float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Todo se calcula en "unidades de pantalla" (0 = arriba del quad, 1 = abajo),
                // igual que el HTML. UV de Unity trae v=0 abajo, así que invertimos acá.
                float x = i.uv.x;
                float y = 1.0 - i.uv.y;

                // --- Paso 2 invertido: arco esférico (columna por columna) ---
                float xn  = (x - 0.5) * 2.0;
                float off = _Drop * xn * xn;
                float span = _FootY - _HorizonY;
                float sc = (span - off) / span;

                float ly; // posición en el "layer" (antes del arco), 0 = arriba, 1 = abajo
                if (y < _FootY)
                    ly = _FootY - (_FootY - y) / sc;
                else
                    ly = y;

                // --- Paso 1 invertido: desenrollar el cilindro ---
                float sinTh = (_FootY - ly) / _Radius;
                if (sinTh > 1.0 || sinTh < -1.0)
                    return fixed4(0,0,0,0); // más allá del horizonte -> transparente (se ve el cielo)

                float th    = asin(sinTh);
                float cosTh = cos(th);
                float vDepth = th * _Radius;

                float sourceY = _FootY - vDepth - _Scroll + _ScrollMax; // en "alturas de pantalla" (+_ScrollMax como colchón para que nunca sea negativo)

                float atlasV  = sourceY / _MapScale;

                float stretch = 1.0 + _XStretch * cosTh;
                float u = (x - 0.5) / stretch + 0.5;

                // Si _RepeatV está prendido, atlasV NO se recorta a [0,1] a propósito — con
                // Wrap Mode = Repeat en la textura, se repite sola en vez de cortarse/estirarse.
                bool vOutOfBounds = (_RepeatV < 0.5) && (atlasV < 0.0 || atlasV > 1.0);
                if (u < 0.0 || u > 1.0 || vOutOfBounds)
                    return fixed4(0,0,0,0); // fuera del atlas -> transparente

                return tex2D(_MainTex, float2(u, 1.0 - atlasV));
            }
            ENDCG
        }
    }
}
