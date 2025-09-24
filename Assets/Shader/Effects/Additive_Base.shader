Shader "Custom/URP_Particle_Additive_Glow"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Glow_Intensity ("Glow Intensity", Float) = 1
        _BaseColor ("Base Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _MainTex_ST;
            float _Glow_Intensity;
            float4 _BaseColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
                float3 normalWS    : TEXCOORD1;
                float3 posWS       : TEXCOORD2;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.posWS = TransformObjectToWorld(v.positionOS.xyz);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                // Base texture & color
                float4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float3 baseColor = texCol.rgb * _BaseColor.rgb * i.color.rgb;

                // Main directional light (Lambert)
                Light mainLight = GetMainLight();
                float3 normal = normalize(i.normalWS);
                float NdotL = saturate(dot(normal, mainLight.direction));
                float3 litColor = baseColor * mainLight.color * NdotL;

                // Emission (self glow)
                float3 emission = baseColor * _Glow_Intensity;

                // Fresnel glow (edge highlight)
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.posWS);
                float fresnel = pow(1.0 - saturate(dot(viewDir, normal)), 3.0);
                float3 fresnelGlow = baseColor * fresnel * _Glow_Intensity;

                // Final HDR color (for bloom to catch)
                float3 finalColor = (litColor + emission + fresnelGlow) * i.color.a;

                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}
