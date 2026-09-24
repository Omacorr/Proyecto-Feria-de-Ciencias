Shader "Custom/EyelidOverlay"
{
    // Parpados del protagonista para ScreenBlink (Assets/Scripts/Ambient/).
    // Se dibuja en una esfera chica alrededor de la camara (no un Canvas: un
    // Screen Space Overlay no se ve en estereo Cardboard, gotcha #7). El borde
    // de los parpados se calcula en espacio de PANTALLA de cada ojo, asi que
    // cierran desde arriba y desde abajo sin importar hacia donde mire el
    // jugador, con un borde curvo (forma de ojo) y suave.
    //
    // _Closed: 0 = ojos abiertos (no tapa nada), 1 = cerrados (todo del color).
    // ZTest Always + cola Overlay+998 (4998): tapa toda la geometria y el
    // reticle (cola Overlay = 4000), pero queda por debajo del desmayo de
    // FaintOverlay (colas 4999/5000). Cull Front: la camara esta ADENTRO de la
    // esfera, asi que se dibuja una sola capa (las caras interiores).
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 1)
        _Closed ("Cerrado (0 abierto, 1 cerrado)", Range(0, 1)) = 0
        _Softness ("Borde suave", Range(0.005, 0.5)) = 0.12
        _Curve ("Curvatura del parpado", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay+998"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Front
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Closed;
                float _Softness;
                float _Curve;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // -1..1 en la imagen de ESTE ojo (0 = centro).
                float2 p = GetNormalizedScreenSpaceUV(IN.positionHCS) * 2.0 - 1.0;
                float open = 1.0 - saturate(_Closed);
                // Media apertura vertical en esta columna: mas alta en el centro
                // (forma de ojo). Con open=1 supera 1 en toda la pantalla (no tapa
                // nada); con open=0 es negativa en toda la pantalla (tapa todo).
                float edge = lerp(-_Softness, 1.0 + _Curve + _Softness, open) - _Curve * p.x * p.x;
                float a = smoothstep(-_Softness, _Softness, abs(p.y) - edge);
                return float4(_Color.rgb, a * _Color.a);
            }
            ENDHLSL
        }
    }
}
