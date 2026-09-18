Shader "Coralia/CurvedWorldUnlit"
{
    // Shader único del mapa: lo usan el suelo, el camino, los nodos y las decoraciones.
    //
    // Unlit a propósito. El mapa no tiene luces ni sombras en tiempo real — el arte ya viene con
    // su sombreado pintado, y en móvil el cuello de botella es cuántos píxeles se pintan, no
    // cuántos triángulos hay. Una luz direccional con sombras obliga a dibujar cada objeto dos
    // veces (shadow map + pasada normal) para un efecto que el arte ya resuelve.
    //
    // La curva vive en CurvedWorld.hlsl, compartida. Acá solo se aplica.
    Properties
    {
        [MainTexture] _BaseMap   ("Textura", 2D) = "white" {}
        [MainColor]   _BaseColor ("Tinte", Color) = (1,1,1,1)
        _Cutoff ("Corte de alpha", Range(0,1)) = 0.001

        // Los sprites se dibujan en la cola de transparentes y sin escribir profundidad; el suelo
        // es opaco y sí escribe. Un mismo shader sirve para los dos cambiando esto por material.
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Blend origen", Float) = 5   // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Blend destino", Float) = 10 // OneMinusSrcAlpha
        [Enum(Off, 0, On, 1)]                   _ZWrite   ("Escribe profundidad", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)]  _Cull     ("Caras", Float) = 2          // Back
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Unlit"

            // Sin este tag URP no dibuja el pass: el renderer recorre solo los LightMode que
            // conoce y descarta el resto en silencio — ni error en consola ni material rosa,
            // simplemente no aparece nada.
            Tags { "LightMode" = "UniversalForward" }

            Blend  [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull   [_Cull]

            HLSLPROGRAM
            #pragma vertex   Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CurvedWorld.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _Cutoff;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                // La curva se aplica en espacio de MUNDO, no de objeto: así dos objetos a la
                // misma distancia se doblan igual sin importar dónde esté su pivote ni cómo esté
                // rotado su transform.
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS = CurveWorldPos(positionWS);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color      = input.color;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor * input.color;
                clip(color.a - _Cutoff);
                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
