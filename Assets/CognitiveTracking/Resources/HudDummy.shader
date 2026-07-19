// Unlit, view-space shaded HUD shader for the cognitive-tracking mirror dummy.
// - ZTest is configurable (Always = draw over world geometry, HUD style).
// - Shading uses a fixed view-space key light + rim so the mannequin reads as 3D
//   regardless of scene lighting. Works in URP (SRPDefaultUnlit pass) and
//   supports single-pass instanced VR rendering.
Shader "TeamSentinels/HudDummy"
{
    Properties
    {
        _Color ("Color", Color) = (0.15, 0.85, 0.3, 1)
        _RimColor ("Rim Color", Color) = (0.65, 1, 0.75, 1)
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 8
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Opaque" "IgnoreProjector"="True" }
        Pass
        {
            ZTest [_ZTest]
            ZWrite On
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 viewNormal : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            fixed4 _RimColor;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewNormal = mul((float3x3)UNITY_MATRIX_V, worldNormal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.viewNormal);
                // Key light from the viewer's upper-left; half-lambert keeps
                // the dark side readable.
                float3 l = normalize(float3(-0.35, 0.75, 0.55));
                float diff = saturate(dot(n, l)) * 0.6 + 0.4;
                // View-space rim highlights the silhouette.
                float rim = pow(1.0 - saturate(n.z), 2.5);
                fixed3 col = _Color.rgb * diff + _RimColor.rgb * rim * 0.45;
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    FallBack "Unlit/Color"
}
