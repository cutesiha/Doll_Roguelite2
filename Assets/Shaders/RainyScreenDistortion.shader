Shader "Hidden/RainyScreenDistortion"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _NoiseTex ("Noise", 2D) = "gray" {}
        _Strength ("Distortion Strength", Range(0, 0.08)) = 0.018
        _Speed ("Distortion Speed", Float) = 0.22
        _Scale ("Distortion Scale", Float) = 5.5
        _TimeOffset ("Time Offset", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            float _Strength;
            float _Speed;
            float _Scale;
            float _TimeOffset;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = _TimeOffset * _Speed;
                float2 noiseUvA = input.uv * _Scale + float2(t * 0.17, t * 0.11);
                float2 noiseUvB = input.uv * (_Scale * 0.63) + float2(-t * 0.08, t * 0.15);
                float2 nA = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUvA).rg * 2.0 - 1.0;
                float2 nB = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUvB).rg * 2.0 - 1.0;
                float2 verticalBias = float2(0.35, 1.0);
                float2 offset = (nA * 0.65 + nB * 0.35) * verticalBias * _Strength;
                float2 uv = saturate(input.uv + offset);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
            }
            ENDHLSL
        }
    }
}
