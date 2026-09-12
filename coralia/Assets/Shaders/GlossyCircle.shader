Shader "Coralia/GlossyCircle"
{
    // Círculo "gomita/vidrioso" procedural — degradado radial (claro arriba-izquierda, oscuro
    // abajo-derecha) + un brillo falso (blob blanco suave) — sin sprite nuevo, tintable con
    // cualquier color. Pensado para el fondo de LevelNodeView (spriteDefault/Current/Gold se
    // reemplazan por este Material + un Color distinto por estado).
    Properties
    {
        _Color         ("Color base", Color) = (0.95, 0.55, 0.72, 1)
        _ShadeAmount   ("Qué tan oscuro el lado abajo-derecha", Range(0,1)) = 0.35
        _HighlightPos  ("Centro del brillo (UV, 0..1)", Vector) = (0.32, 0.72, 0, 0)
        _HighlightSize ("Radio del brillo", Range(0,0.5)) = 0.14
        _HighlightSoft ("Suavidad del borde del brillo", Range(0.01,0.5)) = 0.12
        _HighlightAlpha("Opacidad del brillo", Range(0,1)) = 0.85
        _RimColor      ("Color del borde/anillo (cara superior, parejo)", Color) = (0.85, 0.65, 0.35, 1)
        _RimWidth      ("Ancho del borde de la cara superior", Range(0,0.2)) = 0.06
        _EdgeColor     ("Color del canto/lateral (grosor de la moneda)", Color) = (0.65, 0.45, 0.2, 1)
        _EdgeHeight    ("Qué tan alto se ve el canto abajo", Range(0,0.4)) = 0.12
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float  _ShadeAmount;
            float4 _HighlightPos;
            float  _HighlightSize, _HighlightSoft, _HighlightAlpha;
            fixed4 _RimColor;
            float  _RimWidth;
            fixed4 _EdgeColor;
            float  _EdgeHeight;

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
                float2 uv = i.uv;
                float2 c  = uv - 0.5;
                float  d  = length(c) * 2.0; // silueta TOTAL (incluye el canto) — 0 = centro, 1 = borde

                float circleAlpha = 1.0 - smoothstep(0.96, 1.0, d);
                if (circleAlpha <= 0.001) return fixed4(0,0,0,0);

                // La "cara de arriba" es el mismo círculo pero corrido hacia arriba — así el
                // canto/lateral (grosor de la moneda) queda expuesto solo abajo, más grueso
                // cuanto más grande sea _EdgeHeight. Es la clave del efecto "no es plano".
                float2 faceC = c - float2(0.0, _EdgeHeight);
                float  faceD = length(faceC) * 2.0;
                float  faceAlpha = 1.0 - smoothstep(0.9, 1.0, faceD);

                // Degradado radial "iluminado desde arriba-izquierda" en la cara de arriba —
                // simula una esfera vidriosa, sin necesitar luces/normales reales.
                float2 lightDir = normalize(float2(-1, 1));
                float  shadeT   = dot(faceC, -lightDir) * 0.5 + 0.5; // 0 = lado claro, 1 = lado oscuro
                fixed3 faceCol = lerp(_Color.rgb * (1.0 + _ShadeAmount * 0.3), _Color.rgb * (1.0 - _ShadeAmount), shadeT);

                // Anillo/borde tipo "galletita" alrededor de la cara de arriba.
                float rim = smoothstep(0.9 - _RimWidth, 0.94 - _RimWidth, faceD);
                faceCol = lerp(faceCol, _RimColor.rgb, rim);

                // Brillo (reflejo falso) cerca de la esquina clara.
                float distToHighlight = length(uv - _HighlightPos.xy);
                float highlight = 1.0 - smoothstep(_HighlightSize - _HighlightSoft, _HighlightSize, distToHighlight);
                faceCol = lerp(faceCol, fixed3(1,1,1), highlight * _HighlightAlpha);

                // Canto/lateral: dentro de la silueta total pero fuera de la cara de arriba.
                fixed3 col = lerp(_EdgeColor.rgb, faceCol, faceAlpha);

                return fixed4(col, _Color.a * circleAlpha);
            }
            ENDCG
        }
    }
}
