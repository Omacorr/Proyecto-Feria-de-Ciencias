Shader "Custom/PortalMark"
{
    // Marca de los portales (teletransporte) y piezas chicas de su decorado
    // (llama de la velita, polillas, la velita misma). Basado en
    // FakeLightPool.shader: quad sin luz, sin sombras, sin z-fighting.
    //
    // - Textura con alfa * _Color * _Intensity, alfa final * _Alpha (lo mueve
    //   PortalVisual por MaterialPropertyBlock: fundido por distancia, brillo
    //   al mirar, modo sutil). Tambien multiplica por el color de vertice.
    // - Mezcla elegible por material (_SrcBlend/_DstBlend):
    //     aditivo   = SrcAlpha / One              (marcas que "brillan")
    //     alfa      = SrcAlpha / OneMinusSrcAlpha (polillas, confeti, vela)
    // - _Cull y _ZWrite por material: las marcas van Cull Off + ZWrite Off;
    //   la vela (cilindro) va Cull Back + ZWrite On para que no se vea por dentro.
    // - Offset: no pelea con el piso.
    // - Niebla opcional (_FogAmount 0..1): en Parte 5 (niebla lineal 5-22 m)
    //   conviene 1 para que la marca se pierda con el fondo. En aditivo la
    //   niebla funde a NEGRO (sumar el color de la niebla iluminaria de mas).
    // - Sin instancing estereo: Cardboard va en multi-pass (ver M6 en CLAUDE.md).
    Properties
    {
        _MainTex ("Textura (con alfa)", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensidad", Float) = 1
        _Alpha ("Alfa", Range(0, 1)) = 1
        _FogAmount ("Niebla (0 = ignora, 1 = completa)", Range(0, 1)) = 0
        [Toggle] _FogToBlack ("Niebla funde a negro (aditivo)", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 0
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
        Blend [_SrcBlend] [_DstBlend]
        Cull [_Cull]
        ZWrite [_ZWrite]
        ZTest LEqual
        Offset -1, -1

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float fogCoord : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Intensity;
                float _Alpha;
                float _FogAmount;
                float _FogToBlack;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                OUT.fogCoord = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float3 col = tex.rgb * _Color.rgb * IN.color.rgb * _Intensity;
                float a = saturate(tex.a * _Color.a * IN.color.a * _Alpha);

                float3 fogged = lerp(MixFog(col, IN.fogCoord), MixFogColor(col, float3(0, 0, 0), IN.fogCoord), _FogToBlack);
                col = lerp(col, fogged, _FogAmount);
                return float4(col, a);
            }
            ENDHLSL
        }
    }
}
