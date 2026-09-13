// 바다 물결 — 원랜디 맵의 섬 사이 바다(MapLayout.SeaSize 1600).
//
// 무엇을 그리나
//   · 큰 너울: Gerstner 파도 3개로 정점을 움직인다. 섬 윗면(IslandTop 1)을 넘으면 안 되니 높이는 합쳐 0.5 이하로
//     낮게 두고, 대신 **명암용 기울기만 따로 키운다**(_SwellShading). 내려다보는 RTS 카메라에선 높이보다
//     빛 받는 면이 달라지는 게 물결로 읽힌다.
//   · 잔물결: 픽셀마다 흐르는 노이즈 두 겹으로 법선을 흔든다. 텍스처가 없어도 된다(물 텍스처는 아직 없다).
//   · 깊이 색: 카메라 깊이 텍스처로 물 밑 지형까지의 거리를 재서 얕은 곳은 청록, 깊은 곳은 남색.
//   · 물가 거품: 섬 절벽과 수면이 만나는 곳(깊이 차 0 근처)에 끊긴 흰 띠.
// ⚠️ 깊이 텍스처는 PC_RPAsset만 켜져 있다(Mobile은 꺼짐). 꺼진 쪽에선 깊이 색·거품 없이 깊은 색 한 톤으로 나온다.
Shader "GuilRandomDefense/SeaWater"
{
    Properties
    {
        _ShallowColor ("얕은 물 색", Color) = (0.20, 0.64, 0.66, 0.70)
        _DeepColor ("깊은 물 색", Color) = (0.03, 0.19, 0.34, 0.94)
        _DepthMaxDistance ("깊은 색이 다 되는 깊이", Float) = 8
        _FoamColor ("물거품 색", Color) = (0.94, 0.97, 1.0, 1.0)
        _FoamDistance ("물가 거품 폭", Float) = 1.6
        _FoamNoiseScale ("거품 무늬 크기", Float) = 0.45
        _WaveA ("너울 A (방향 xy, 가파름, 파장)", Vector) = (1.0, 0.3, 0.016, 90)
        _WaveB ("너울 B", Vector) = (-0.4, 1.0, 0.018, 57)
        _WaveC ("너울 C", Vector) = (0.7, -0.8, 0.020, 33)
        _WaveSpeedScale ("너울 속도 배율", Float) = 0.6
        _SwellShading ("너울 명암 배율", Float) = 9
        _RippleScale ("잔물결 촘촘함", Float) = 0.14
        _RippleStrength ("잔물결 세기", Range(0, 2)) = 0.8
        _RippleSpeed ("잔물결 속도", Float) = 0.5
        _Smoothness ("반짝임", Range(0, 1)) = 0.88
        _FresnelPower ("가장자리 반사", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "SeaForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // SRP Batcher — 재질 값은 전부 이 버퍼 안에 있어야 한다.
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthMaxDistance;
                half4 _FoamColor;
                float _FoamDistance;
                float _FoamNoiseScale;
                float4 _WaveA;
                float4 _WaveB;
                float4 _WaveC;
                float _WaveSpeedScale;
                float _SwellShading;
                float _RippleScale;
                float _RippleStrength;
                float _RippleSpeed;
                float _Smoothness;
                float _FresnelPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
            };

            // wave.xy = 진행 방향, wave.z = 가파름(0~1), wave.w = 파장(게임 단위).
            // 정점 이동은 실제 가파름으로, 법선용 기울기는 _SwellShading을 곱해 따로 쌓는다.
            float3 GerstnerWave(float4 wave, float3 p, inout float3 tangent, inout float3 binormal)
            {
                float k = TWO_PI / max(wave.w, 0.001);
                float c = sqrt(9.8 / k) * _WaveSpeedScale;
                float2 d = normalize(wave.xy);
                float f = k * (dot(d, p.xz) - c * _Time.y);
                float a = wave.z / k;

                float s = wave.z * _SwellShading;
                float sinF = sin(f);
                float cosF = cos(f);
                tangent  += float3(-d.x * d.x * s * sinF, d.x * s * cosF, -d.x * d.y * s * sinF);
                binormal += float3(-d.x * d.y * s * sinF, d.y * s * cosF, -d.y * d.y * s * sinF);

                return float3(d.x * a * cosF, a * sinF, d.y * a * cosF);
            }

            float2 GradientHash(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453);
            }

            float GradientNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float n00 = dot(GradientHash(i), f);
                float n10 = dot(GradientHash(i + float2(1, 0)), f - float2(1, 0));
                float n01 = dot(GradientHash(i + float2(0, 1)), f - float2(0, 1));
                float n11 = dot(GradientHash(i + float2(1, 1)), f - float2(1, 1));
                return lerp(lerp(n00, n10, u.x), lerp(n01, n11, u.x), u.y);
            }

            // 방향이 다른 두 겹이 엇갈려 흘러야 한쪽으로 미끄러지는 느낌이 안 난다.
            float RippleHeight(float2 xz)
            {
                float t = _Time.y * _RippleSpeed;
                float h = GradientNoise(xz * _RippleScale + float2(t, t * 0.6));
                h += 0.5 * GradientNoise(xz * _RippleScale * 2.3 + float2(-t * 0.8, t * 0.9));
                return h;
            }

            // 원근·직교 카메라 둘 다에서 눈 기준 거리로 바꾼다.
            float SceneEyeDepth(float2 uv)
            {
                float raw = SampleSceneDepth(uv);
                if (unity_OrthoParams.w > 0.5)
                {
                #if UNITY_REVERSED_Z
                    raw = 1.0 - raw;
                #endif
                    return lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
                }
                return LinearEyeDepth(raw, _ZBufferParams);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 grid = TransformObjectToWorld(input.positionOS.xyz);
                float3 tangent = float3(1, 0, 0);
                float3 binormal = float3(0, 0, 1);

                float3 positionWS = grid;
                positionWS += GerstnerWave(_WaveA, grid, tangent, binormal);
                positionWS += GerstnerWave(_WaveB, grid, tangent, binormal);
                positionWS += GerstnerWave(_WaveC, grid, tangent, binormal);

                output.positionWS = positionWS;
                output.normalWS = normalize(cross(binormal, tangent));
                output.positionCS = TransformWorldToHClip(positionWS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 잔물결 — 옆 두 점과의 높이 차로 기울기를 잰다.
                float2 xz = input.positionWS.xz;
                float e = 0.35;
                float h0 = RippleHeight(xz);
                float hx = RippleHeight(xz + float2(e, 0));
                float hz = RippleHeight(xz + float2(0, e));
                float2 slope = float2(hx - h0, hz - h0) / e * _RippleStrength;

                float3 n = normalize(input.normalWS);
                float3 normalWS = normalize(float3(n.x - slope.x, n.y, n.z - slope.y));
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));

                // 물 깊이 — 수면에서 그 뒤 불투명 지형까지.
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float surfaceDepth = -TransformWorldToView(input.positionWS).z;
                float waterDepth = max(0.0, SceneEyeDepth(screenUV) - surfaceDepth);

                half4 water = lerp(_ShallowColor, _DeepColor, saturate(waterDepth / max(_DepthMaxDistance, 0.001)));

                // 물가 거품 — 흐르는 노이즈를 문턱으로 써서 끊긴 띠로 만든다.
                float foamMask = 1.0 - saturate(waterDepth / max(_FoamDistance, 0.001));
                float foamNoise = GradientNoise(xz * _FoamNoiseScale + _Time.y * 0.25) * 0.5 + 0.5;
                float foam = smoothstep(foamNoise - 0.06, foamNoise + 0.06, foamMask);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = mainLight.shadowAttenuation;
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float nDotL = saturate(dot(normalWS, mainLight.direction));
                float specPower = exp2(_Smoothness * 10.0 + 1.0);
                float spec = pow(saturate(dot(normalWS, halfDir)), specPower) * _Smoothness;
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                half3 ambient = SampleSH(normalWS);

                half3 color = water.rgb * (ambient + mainLight.color * (0.35 + 0.65 * nDotL) * shadow);
                color += fresnel * ambient * 0.6;                        // 하늘 반사 근사
                color += spec * mainLight.color * shadow;                 // 해 반짝임
                color = lerp(color, _FoamColor.rgb * (ambient + mainLight.color * 0.8 * shadow), foam);

                half alpha = saturate(max(water.a, max(foam, fresnel * 0.5)));
                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
