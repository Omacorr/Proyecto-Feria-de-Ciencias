Shader "Custom/FakeLightPool"
{
    // "Charco de luz" falso: un quad plano en el piso con un degradado radial
    // ADITIVO calculado en el shader a partir de las UV (no usa textura).
    // Simula el halo que una luz deja en el suelo sin gastar una Light real
    // (en el celular, nivel Mobile/Forward, cada objeto solo recibe 4 luces).
    //
    // - Blend One One: suma luz sobre lo que haya detras; negro = no cambia
    //   nada, asi que no hace falta alfa.
    // - Sin luz, sin sombras y SIN niebla (a proposito: el charco es
    //   emisivo; la niebla de la escena esta apagada en las Partes 1-5).
    // - ZWrite Off + Offset: no pelea (z-fighting) con el piso.
    // - Cull Off: se ve de los dos lados, y se escribe igual que
    //   Abyss_Void.shader (sin instancing estereo: multi-pass, ver M6 en
    //   CLAUDE.md "QA de build Android / A54").
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.85, 0.5, 1)
        _Intensity ("Intensidad", Float) = 0.35
        _Falloff ("Caida (mayor = mas concentrado)", Float) = 2
        _Core ("Brillo del centro", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        Blend One One
        Cull Off
        ZWrite Off
        ZTest LEqual
        Offset -1, -1

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;
                float _Falloff;
                float _Core;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // 0 en el centro, 1 en el borde del circulo inscripto en el quad.
                float d = length(IN.uv - 0.5) * 2.0;
                float t = saturate(1.0 - d);
                // smoothstep: borde sin corte duro; pow: que tan concentrado.
                float f = pow(t * t * (3.0 - 2.0 * t), max(_Falloff, 0.01));
                // Un pequeno nucleo mas brillante en el centro.
                f += _Core * pow(t, 6.0);
                float3 col = _Color.rgb * (_Intensity * f);
                return float4(col, 1);
            }
            ENDHLSL
        }
    }
}
