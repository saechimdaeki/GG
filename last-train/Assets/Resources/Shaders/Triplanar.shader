// World-space triplanar PBR for procedural geometry (streets, walls, floors): textures tile by
// meters instead of stretching over each box. Uses URP's standard PBR lighting, shadows and fog.
Shader "LastTrain/Triplanar"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _ArmMap ("AO/Rough/Metal", 2D) = "white" {}
        _Tile ("Meters per tile", Float) = 2
        _Smoothness ("Smoothness scale", Range(0,1)) = 0.6
        _Wet ("Wetness", Range(0,1)) = 0
        _Gain ("Albedo gain", Float) = 1
        _TexAmount ("Texture amount", Range(0,1)) = 1
        _EmissionColor ("Emission", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_ArmMap); SAMPLER(sampler_ArmMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                float _Tile;
                half _Smoothness;
                half _Wet;
                half _Gain;
                half _TexAmount;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fog : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n = normalize(i.normalWS);
                float3 w = pow(abs(n), 6); w /= (w.x + w.y + w.z);
                float3 p = i.positionWS / _Tile;
                float2 ux = p.zy, uy = p.xz, uz = p.xy;
                half4 a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, ux) * w.x + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uy) * w.y + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uz) * w.z;
                half4 arm = SAMPLE_TEXTURE2D(_ArmMap, sampler_ArmMap, ux) * w.x + SAMPLE_TEXTURE2D(_ArmMap, sampler_ArmMap, uy) * w.y + SAMPLE_TEXTURE2D(_ArmMap, sampler_ArmMap, uz) * w.z;
                // whiteout-blended triplanar normals
                half3 tx = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, ux));
                half3 ty = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uy));
                half3 tz = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uz));
                tx = half3(tx.xy + n.zy, abs(tx.z) * n.x);
                ty = half3(ty.xy + n.xz, abs(ty.z) * n.y);
                tz = half3(tz.xy + n.xy, abs(tz.z) * n.z);
                float3 nw = normalize(tx.zyx * w.x + ty.xzy * w.y + tz.xyz * w.z);

                half wet = _Wet * saturate(n.y);
                SurfaceData s = (SurfaceData)0;
                s.albedo = lerp(1, saturate(a.rgb * _Gain), _TexAmount) * _BaseColor.rgb * lerp(1, 0.55, wet);
                s.alpha = 1;
                s.metallic = arm.b;
                s.smoothness = saturate((1 - arm.g) * _Smoothness + wet * 0.55);
                s.occlusion = arm.r;
                s.emission = _EmissionColor.rgb;
                s.normalTS = half3(0, 0, 1);

                InputData d = (InputData)0;
                d.positionWS = i.positionWS;
                d.positionCS = i.positionCS;
                d.normalWS = normalize(lerp(nw, n, wet * 0.7));
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.fogCoord = i.fog;
                d.bakedGI = SampleSH(d.normalWS);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                d.shadowMask = half4(1, 1, 1, 1);
                half4 c = UniversalFragmentPBR(d, s);
                c.rgb = MixFog(c.rgb, i.fog);
                return c;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Universal Render Pipeline/Lit"
}
