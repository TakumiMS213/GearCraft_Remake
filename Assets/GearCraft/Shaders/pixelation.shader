Shader "GearCraft/Pixelation"
{
    Properties
    {
        [HideInInspector] _BlitTexture ("Blit Texture", 2D) = "white" {}
        _MainTex ("Main Texture", 2D) = "white" {}
        _UseMainTex ("Use Main Texture", Float) = 0
        _PixelSize ("Pixel Size", Range(1, 128)) = 6
        _Strength ("Strength", Range(0, 1)) = 1
        _ColorSteps ("Color Steps", Range(0, 64)) = 0
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Overlay"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Pixelation"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);
            float4 _BlitTexture_TexelSize;

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float _UseMainTex;
                float _PixelSize;
                float _Strength;
                float _ColorSteps;
                float4 _Tint;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);

                return output;
            }

            float2 PixelateUV(float2 uv, float4 texelSize)
            {
                float2 textureSize = max(texelSize.zw, float2(1.0, 1.0));
                float pixelSize = max(_PixelSize, 1.0);
                float2 pixelCoord = floor(uv * textureSize / pixelSize) * pixelSize + pixelSize * 0.5;
                return saturate(pixelCoord / textureSize);
            }

            half4 Posterize(half4 color)
            {
                if (_ColorSteps <= 1.0)
                {
                    return color;
                }

                half steps = (half)_ColorSteps;
                color.rgb = floor(color.rgb * steps) / steps;
                return color;
            }

            half4 SampleSource(float2 uv)
            {
                if (_UseMainTex > 0.5)
                {
                    float2 pixelUv = PixelateUV(uv, _MainTex_TexelSize);
                    half4 normalColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                    half4 pixelColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, pixelUv);
                    return lerp(normalColor, pixelColor, saturate(_Strength));
                }

                float2 blitPixelUv = PixelateUV(uv, _BlitTexture_TexelSize);
                half4 blitNormalColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);
                half4 blitPixelColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, blitPixelUv);
                return lerp(blitNormalColor, blitPixelColor, saturate(_Strength));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 color = SampleSource(input.uv);
                color = Posterize(color);
                color *= (half4)_Tint;
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
