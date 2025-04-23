Shader "Unlit/GrassWind"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _WindSpeed ("Wind Speed", Float) = 1
        _WindStrength ("Wind Strength", Float) = 0.1
        _WindDirection ("Wind Direction (XYZ)", Vector) = (1,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _WindSpeed;
            float _WindStrength;
            float3 _WindDirection;

            v2f vert (appdata v)
            {
                v2f o;
                float time = _Time.y; // Unity's time in seconds
                float swayFactor = sin(v.vertex.x * _WindSpeed + time) * _WindStrength;
                float3 swayedPosition = v.vertex.xyz + _WindDirection * swayFactor;
                o.vertex = UnityObjectToClipPos(float4(swayedPosition, 1.0));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                return col;
            }
            ENDCG
        }
    }
}