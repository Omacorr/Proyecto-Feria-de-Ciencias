Shader "Custom/AbyssVoid"
{
    // Color plano, opaco, sin luz y (por defecto) SIN niebla.
    //
    // Usos:
    //  - Paredes del pozo del abismo (AbyssFall, Parte 3): negro absoluto. Con
    //    un Unlit comun de URP, la niebla gris de la escena pintaria de gris
    //    las paredes lejanas y el pozo dejaria de verse "infinito".
    //  - Panel brillante de WallCollapse (Parte 4): color HDR (>1) que hace
    //    reaccionar al Bloom.
    //  - Emisivo barato para cualquier cosa que tenga que "brillar" sin gastar
    //    una Light: foco rojo de la mirilla (Parte 3), tubos fluorescentes
    //    rotos de Backrooms (Parte 5). El color es [HDR]: en el selector de
    //    color subir "Intensity" para que brille con Bloom.
    //
    // "Aplicar niebla" (apagado por defecto) mezcla la niebla de la escena
    // (Lighting > Environment > Fog). Dejarlo APAGADO en el pozo del abismo;
    // prenderlo en emisivos lejanos dentro de niebla (Backrooms) para que no
    // "floten" por delante de la niebla.
    //
    // Escribe profundidad normal (ZWrite On), asi que tapa todo lo que quede
    // detras, pero NO pasa por encima de nada (a diferencia de UI/AlwaysOnTop).
    // Cull Off: se ve de los dos lados (el Quad de Unity mira hacia -Z).
    Properties
    {
        [HDR] _Color ("Color", Color) = (0, 0, 0, 1)
        [Toggle(_ABYSS_FOG_ON)] _ApplyFog ("Aplicar niebla", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        Cull Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ABYSS_FOG_ON
            // Mismas pragmas de niebla que usa el Unlit de URP 17.
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            #if defined(_ABYSS_FOG_ON)
                float fogFactor : TEXCOORD0;
            #endif
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _ApplyFog;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
            #if defined(_ABYSS_FOG_ON)
                OUT.fogFactor = ComputeFogFactor(OUT.positionHCS.z);
            #endif
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 col = _Color.rgb;
            #if defined(_ABYSS_FOG_ON)
                col = MixFog(col, IN.fogFactor);
            #endif
                return float4(col, 1);
            }
            ENDHLSL
        }
    }
}
