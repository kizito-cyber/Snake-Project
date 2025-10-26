Shader "Custom/URPUnlit/SpriteOutline"
{
    Properties
    {
        [MainTexture]_MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Main Color", Color) = (0,0,0,1) // Black
        _OutlineColor("Outline Color", Color) = (1,1,0,1) // Yellow
        _OutlineThickness("Outline Thickness (pixels)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="Universal2D" }

            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

            float4 _Color;
            float4 _OutlineColor;
            float _OutlineThickness;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // Get texture size to calculate texel size
                uint width, height;
                _MainTex.GetDimensions(width, height);
                float2 texel = _OutlineThickness / float2(width, height);

                // Sample alpha from neighboring pixels
                float a0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-texel.x, 0)).a;
                float a1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(texel.x, 0)).a;
                float a2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0, -texel.y)).a;
                float a3 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0, texel.y)).a;
                float maxNeighborAlpha = max(max(a0, a1), max(a2, a3));

                float centerAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;

                float outlineMask = saturate(maxNeighborAlpha - centerAlpha);

                float4 result = _OutlineColor;
                result.a *= outlineMask;

                return result;
            }
            ENDHLSL
        }

        Pass
        {
            Name "FILL"
            Tags { "LightMode"="Universal2D" }

            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag_fill
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

            float4 _Color;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            float4 frag_fill(Varyings IN) : SV_Target
            {
                float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                return float4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
