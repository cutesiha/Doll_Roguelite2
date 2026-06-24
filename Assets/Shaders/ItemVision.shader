Shader "Hidden/DollRoguelite/ItemVision"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Mode ("Mode", Float) = 0
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
                    float2 p = input.uv * 2.0 - 1.0;
                    p.x *= _ScreenParams.x / max(1.0, _ScreenParams.y);
                    float2 c0 = float2(-0.42, 0.28);
                    float2 c1 = float2(0.42, 0.28);
                    float2 c2 = float2(-0.42, -0.28);
                    float2 c3 = float2(0.42, -0.28);
                    float mask = step(length(p - c0), 0.31);
                    mask = max(mask, step(length(p - c1), 0.31));
                    mask = max(mask, step(length(p - c2), 0.31));
                    mask = max(mask, step(length(p - c3), 0.31));
                    half4 button = half4(0.12, 0.055, 0.025, 1.0);
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
