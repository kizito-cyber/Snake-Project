Shader "Unlit/SnakeIconCircles"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _IconsPerU    ("Icons Per U?Unit",    Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            float  _IconsPerU;

            struct appdata {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;    // uv.x maps along the length of the line
                                            // uv.y maps across the thickness of the line
            };
            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                // tile one circle every 1/_IconsPerU world?units
                float u = frac(i.uv.x * _IconsPerU);

                // reconstruct per?tile UV in [0,1]×[0,1]
                float2 tileUV = float2(u, i.uv.y);

                // center each tile at (0.5, 0.5)
                float2 centered = tileUV - 0.5;

                // radius = 0.5 ? perfect circle
                float mask = step(length(centered), 0.5);

                // output outline color where mask==1, transparent elsewhere
                return fixed4(_OutlineColor.rgb, _OutlineColor.a * mask);
            }
            ENDCG
        }
    }
}
