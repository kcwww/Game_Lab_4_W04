Shader "Hidden/URP/RadialZoomBlur_Simple"
{
    Properties
    {
        // 위치/구도
        _Center        ("Center (UV)", Vector) = (0.5, 0.5, 0, 0)
        _CenterOffsetY ("Center Offset Y", Float) = 0.0

        // 줌 블러 샘플링(Shadertoy 방식)
        _BlurStart     ("Blur Start", Float) = 1.0
        _BlurWidth     ("Blur Width", Float) = 0.10
        _SampleCount   ("Sample Count", Int) = 10

        // 강도(샘플 수와 독립)
        _Strength      ("Blend Strength", Range(0,1)) = 1.0

        // 반경 마스킹
        _Radius        ("Radius", Range(0,1)) = 0.35
        _Feather       ("Radius Feather", Range(0,0.5)) = 0.10
        _Invert        ("Invert (Center Blur)", Float) = 0.0 // 0=바깥 블러, 1=중심 블러
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Opaque" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "RadialZoomBlur_Simple"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Fullscreen Pass source
            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            // Controls
            float2 _Center;
            float  _CenterOffsetY;

            float  _BlurStart;
            float  _BlurWidth;
            int    _SampleCount;

            float  _Strength;   // 0..1

            // Radius mask
            float  _Radius;     // 0..1 (UV 거리, 종횡비 보정 적용)
            float  _Feather;    // 0..0.5
            float  _Invert;     // 0 or 1

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(uint id : SV_VertexID)
            {
                Varyings o;
                o.positionCS = GetFullScreenTriangleVertexPosition(id);
                o.uv         = GetFullScreenTriangleTexCoord(id);
                return o;
            }

            float3 SampleSrc(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv).rgb;
            }

            // 원형 유지 위해 종횡비 보정 거리
            float AspectDistance(float2 fromCenter)
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 a = float2(fromCenter.x * aspect, fromCenter.y);
                return length(a);
            }

            half4 frag(Varyings i) : SV_Target
            {
                // 중심점/오프셋
                float2 center = float2(_Center.x, _Center.y + _CenterOffsetY);
                float2 duv    = i.uv - center;

                // 샘플 설정
                static const int MAX_SAMPLES = 32;
                int   sc    = clamp(_SampleCount, 1, MAX_SAMPLES);
                float stepW = (sc > 1) ? (_BlurWidth / (float)(sc - 1)) : 0.0;

                float3 src = SampleSrc(i.uv);
                float3 acc = 0.0;

                // 약한 지터(밴딩 완화)
                float jitter = frac(sin(dot(i.uv, float2(12.9898,78.233))) * 43758.5453 + _Time.y) * (1.0 / _ScreenParams.x);

                [unroll]
                for (int s = 0; s < MAX_SAMPLES; s++)
                {
                    if (s >= sc) break;

                    float scale = _BlurStart + (float)s * stepW;       // shadertoy와 동일
                    float2 suv  = center + duv * scale;
                    suv = clamp(suv, 0.0, 1.0);

                    acc += SampleSrc(suv + jitter);
                }

                float3 blurred = acc / (float)sc;

                // ----- 반경 마스크 -----
                // dist: 중심에서의 거리(종횡비 보정)
                float dist = AspectDistance(duv);

                // 바깥만 블러: w_out = smoothstep(R, R-Feather, dist)
                //  - dist <= R-Feather -> 0(선명), dist >= R -> 1(블러)
                float R  = max(_Radius, 1e-5);
                float F  = max(_Feather, 0.0);
                float inner = max(R - F, 0.0);
                float wMask = (F > 1e-6) ? smoothstep(R, inner, dist) : step(R, dist);

                // Invert가 1이면 중심만 블러
                if (_Invert > 0.5)
                {
                    wMask = 1.0 - wMask;
                }

                // 강도 블렌드: 최종 = lerp(src, blurred, wMask * _Strength)
                float w = saturate(wMask * _Strength);
                float3 outCol = lerp(src, blurred, w);

                return float4(outCol, 1);
            }
            ENDHLSL
        }
    }
}
