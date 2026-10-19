// 우리 이펙트 공용 셰이더(10-19 이펙트 개편 E5) — URP 파티클용 Unlit 가산/알파.
// 기능: 가산·알파 섞기 · 파티클 색(startColor·ColorOverLifetime 곱) · 소프트 파티클(Depth Texture, 켜져 있음) ·
//       UV 스크롤 · 노이즈 디졸브(알파 문턱을 키우면 노이즈 얼룩으로 녹듯이 사라짐) · 가장자리 발광 · 변형(왜곡: Opaque Texture 켜져 있음).
// 쓰는 법: 재질을 이 셰이더로 만들고 _BaseMap에 흰색+알파 그림을 건다 — 색은 파티클 startColor가 입힌다(Kenney 텍스처와 같은 규칙).
// 파티클 시스템 렌더러의 Custom Vertex Streams는 안 쓴다(기본: 위치·색·UV만).
Shader "GRD/FxGlow"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture (white + alpha)", 2D) = "white" {}
        [HDR] _Tint ("Tint (multiplied with particle color)", Color) = (1,1,1,1)
        _Intensity ("Intensity", Range(0, 8)) = 1.5
        [Enum(Additive,1,Alpha,10)] _DstBlend ("Blend (1 = additive, 10 = alpha)", Float) = 1
        _SoftFade ("Soft particle fade distance (0 = off)", Range(0, 5)) = 1
        _ScrollX ("UV scroll X", Float) = 0
        _ScrollY ("UV scroll Y", Float) = 0
        _NoiseScale ("Noise scale", Float) = 3
        _Dissolve ("Dissolve (0 = none, 1 = gone)", Range(0, 1)) = 0
        _EdgeWidth ("Dissolve edge width", Range(0, 0.3)) = 0.08
        [HDR] _EdgeColor ("Dissolve edge color", Color) = (0.6,0.8,1,1)
        _Distort ("Distortion strength (needs Opaque Texture)", Range(0, 0.05)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "GRDFxGlow"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Tint;
                half _Intensity;
                half _SoftFade;
                half _ScrollX;
                half _ScrollY;
                half _NoiseScale;
                half _Dissolve;
                half _EdgeWidth;
                half4 _EdgeColor;
                half _Distort;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs vp = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vp.positionCS;
                output.screenPos = ComputeScreenPos(vp.positionCS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap) + float2(_ScrollX, _ScrollY) * _Time.y;
                output.color = input.color;
                return output;
            }

            // 값 노이즈(텍스처 없이) — 디졸브 얼룩용
            float Hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x), lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), f.x), f.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 uv = input.uv;
                float2 screenUV = input.screenPos.xy / input.screenPos.w;

                // 왜곡: 노이즈로 장면(불투명 텍스처)을 흔들어 그린다 — _Distort가 0이면 건너뜀
                half3 sceneBack = 0;
                if (_Distort > 0.0001)
                {
                    float2 n = float2(ValueNoise(uv * _NoiseScale + _Time.y), ValueNoise(uv * _NoiseScale + 17.3 - _Time.y)) - 0.5;
                    sceneBack = SampleSceneColor(screenUV + n * _Distort);
                }

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half a = tex.a;

                // 디졸브: 노이즈 값이 문턱보다 낮은 곳부터 사라지고, 문턱 바로 위에 가장자리 발광
                half edge = 0;
                if (_Dissolve > 0.0001)
                {
                    half nz = ValueNoise(uv * _NoiseScale);
                    half cut = _Dissolve * (1.0 + _EdgeWidth);
                    a *= step(cut - _EdgeWidth, nz) * saturate((nz - (cut - _EdgeWidth)) * 20.0);
                    edge = saturate(1.0 - (nz - (cut - _EdgeWidth)) / max(_EdgeWidth, 0.0001)) * step(cut - _EdgeWidth, nz);
                }

                half3 rgb = tex.rgb * _Tint.rgb * input.color.rgb * _Intensity;
                rgb = lerp(rgb, _EdgeColor.rgb * _Intensity, edge * (_Dissolve > 0.0001 ? 1 : 0));
                if (_Distort > 0.0001) rgb = lerp(sceneBack, rgb, saturate(a));
                half alpha = a * _Tint.a * input.color.a;

                // 소프트 파티클: 뒤 장면 깊이와 가까울수록 투명 — 땅·유닛에 파고들며 생기는 딱딱한 선 제거
                if (_SoftFade > 0.0001)
                {
                    float sceneZ = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                    float partZ = input.screenPos.w;
                    alpha *= saturate((sceneZ - partZ) / _SoftFade);
                }
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
