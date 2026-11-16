// Cheap swaying plant shader for grass, ferns and weeds painted on the terrain.
// - Sways in the wind: the base stays planted, the top moves the most, and each plant is out of step with its neighbours.
// - Supports GPU instancing, so thousands of plants are drawn in a few batches.
// - Unlit with a single light colour fed in by TerrainPolish, which keeps it very cheap per pixel.
// - Alpha cutout for leaf textures, drawn from both sides, with scene fog.
Shader "Ishana/Wind Plant"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _PlantLight ("Light Colour (set by TerrainPolish)", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0
        _BaseDarken ("Darken Toward Base", Range(0,1)) = 0.25
        _BendHeight ("Plant Height (world units)", Float) = 1
        _WindStrength ("Sway Distance At The Top (world units)", Float) = 0.15
        _WindSpeed ("Wind Speed", Float) = 1.5
        _WindScale ("Wind Variation Across The Ground", Float) = 0.35
        _WindDirection ("Wind Direction (XZ)", Vector) = (1,0,0.4,0)
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        LOD 100
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float shade : TEXCOORD1;
                UNITY_FOG_COORDS(2)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _PlantLight;
            float _Cutoff;
            float _BaseDarken;
            float _BendHeight;
            float _WindStrength;
            float _WindSpeed;
            float _WindScale;
            float4 _WindDirection;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 world = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
                float3 root = mul(unity_ObjectToWorld, float4(0.0, 0.0, 0.0, 1.0)).xyz;

                // 0 at the base, 1 at the top. Squared so the stem stays planted and the tip moves.
                float height = saturate((world.y - root.y) / max(_BendHeight, 0.001));
                float bend = height * height;

                // Each plant gets its own phase from where it stands, so gusts roll across the ground
                float phase = _Time.y * _WindSpeed + (root.x + root.z * 0.7) * _WindScale;
                float sway = (sin(phase) + 0.5 * sin(phase * 2.3 + 1.7)) * _WindStrength * bend;

                float2 dir = _WindDirection.xz;
                dir = dir / max(length(dir), 0.001);
                world.xz += dir * sway;

                o.pos = mul(UNITY_MATRIX_VP, float4(world, 1.0));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.shade = lerp(1.0 - _BaseDarken, 1.0, height);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                clip(col.a - _Cutoff);
                col.rgb *= _PlantLight.rgb * i.shade;
                UNITY_APPLY_FOG(i.fogCoord, col);
                col.a = 1.0;
                return col;
            }
            ENDCG
        }
    }
}
