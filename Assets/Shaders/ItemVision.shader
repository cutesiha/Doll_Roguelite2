Shader "Hidden/DollRoguelite/ItemVision"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Mode ("Mode", Float) = 0
        _WidthFraction ("Width Fraction", Float) = 1
        _UOffset ("U Offset", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Cull Off
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
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float _Mode;
            float _WidthFraction;
            float _UOffset;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 scene = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                if (_Mode > 1.5)
                {
                    // RawImage는 uvRect를 정점 UV에 직접 구워버려서 input.uv로는 이 쿼드의 "로컬 0..1 위치"를
                    // 되살릴 수 없다(항상 잘린 반쪽 범위만 나옴). 대신 프래그먼트 셰이더의 SV_POSITION(실제 화면
                    // 픽셀 좌표)을 직접 써서, 이 채널이 담당하는 화면 영역(_UOffset~_UOffset+_WidthFraction) 기준으로
                    // 로컬 -1..1 좌표를 다시 계산한다 — uvRect 크롭과 완전히 무관하게 항상 정확하다.
                    float2 channelOriginPx = float2(_UOffset * _ScreenParams.x, 0.0);
                    float2 channelSizePx = float2(max(1.0, _WidthFraction * _ScreenParams.x), max(1.0, _ScreenParams.y));
                    float2 local = (input.positionHCS.xy - channelOriginPx) / channelSizePx;
                    float2 p = local * 2.0 - 1.0;
                    p.x *= channelSizePx.x / channelSizePx.y;
                    // 반지름 0.24, 중심 간격(가로/세로 각 0.92)이 반지름 합(0.48)보다 훨씬 커서 4개 구멍이 겹치지 않는다.
                    float radius = 0.24;
                    float2 c0 = float2(-0.46, 0.46);
                    float2 c1 = float2(0.46, 0.46);
                    float2 c2 = float2(-0.46, -0.46);
                    float2 c3 = float2(0.46, -0.46);
                    float mask = step(length(p - c0), radius);
                    mask = max(mask, step(length(p - c1), radius));
                    mask = max(mask, step(length(p - c2), radius));
                    mask = max(mask, step(length(p - c3), radius));
                    half4 button = half4(0.0, 0.0, 0.0, 1.0);
                    return lerp(button, scene, mask);
                }
                if (_Mode > 0.5)
                    scene.rgb = 1.0 - scene.rgb;
                return scene;
            }
            ENDHLSL
        }
    }
}
