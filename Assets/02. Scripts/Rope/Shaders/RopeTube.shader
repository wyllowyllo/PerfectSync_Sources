Shader "Custom/RopeTube"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.5
        _FresnelPower("Fresnel Power", Range(1,5)) = 2.0
        _FresnelIntensity("Fresnel Intensity", Range(0,1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // 모든 패스에서 공유하는 로프 정점 계산 로직
        HLSLINCLUDE
        StructuredBuffer<float3> _NodePositions;
        float _Thickness;
        int _PhysicsNodeCount;
        int _InterpolationSegments;
        int _RenderNodeCount;

        float3 CatmullRom(float3 p0, float3 p1, float3 p2, float3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5 * (
                (2.0 * p1) +
                (-p0 + p2) * t +
                (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * t2 +
                (-p0 + 3.0 * p1 - 3.0 * p2 + p3) * t3
            );
        }

        float3 GetInterpolatedPosition(int renderIndex)
        {
            renderIndex = clamp(renderIndex, 0, _RenderNodeCount - 1);
            int physSegment = renderIndex / _InterpolationSegments;
            int subIndex = renderIndex - physSegment * _InterpolationSegments;
            float t = (float)subIndex / (float)_InterpolationSegments;

            if (physSegment >= _PhysicsNodeCount - 1)
            {
                physSegment = _PhysicsNodeCount - 2;
                t = 1.0;
            }

            int i0 = max(0, physSegment - 1);
            int i1 = physSegment;
            int i2 = min(_PhysicsNodeCount - 1, physSegment + 1);
            int i3 = min(_PhysicsNodeCount - 1, physSegment + 2);

            return CatmullRom(
                _NodePositions[i0], _NodePositions[i1],
                _NodePositions[i2], _NodePositions[i3], t);
        }

        // UV로부터 로프 정점의 월드 좌표와 노말을 계산합니다.
        // UV.y = 렌더링 노드 위치 (0~1), UV.x = 원통 단면의 각도 (0~1)
        void ComputeRopeVertex(float2 uv, out float3 worldPos, out float3 normalWS)
        {
            int renderIndex = (int)round(uv.y * (_RenderNodeCount - 1));
            float angle = uv.x * 6.28318530718;

            float3 center = GetInterpolatedPosition(renderIndex);

            // 인접 보간 노드로부터 Forward 방향 계산
            float3 fwd;
            if (renderIndex < _RenderNodeCount - 1)
                fwd = normalize(GetInterpolatedPosition(renderIndex + 1) - center);
            else
                fwd = normalize(center - GetInterpolatedPosition(renderIndex - 1));

            // Forward와 최소 평행한 참조 축으로 Right/Up 프레임 구성
            float3 refUp = abs(fwd.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0);
            float3 right = normalize(cross(refUp, fwd));
            float3 up = cross(fwd, right);

            // 원통 단면 확장
            float radius = _Thickness * 0.5;
            float3 radialDir = right * cos(angle) + up * sin(angle);
            worldPos = center + radialDir * radius;
            normalWS = radialDir;
        }
        ENDHLSL

        // ===== Forward Lit Pass =====
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Smoothness;
                float _FresnelPower;
                float _FresnelIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : COLOR;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 worldPos, normalWS;
                ComputeRopeVertex(input.uv, worldPos, normalWS);

                output.positionCS = TransformWorldToHClip(worldPos);
                output.positionWS = worldPos;
                output.normalWS = normalWS;
                output.uv = input.uv;
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));

                // 메인 라이트
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // 디퓨즈
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float3 diffuse = mainLight.color * mainLight.shadowAttenuation * NdotL;

                // 스페큘러 (Blinn-Phong)
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float specPower = exp2(10.0 * _Smoothness + 1.0);
                float3 specular = mainLight.color * mainLight.shadowAttenuation
                    * pow(NdotH, specPower) * _Smoothness;

                // 프레넬 림 (고무 질감 강조)
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                float3 rim = fresnel * _FresnelIntensity * mainLight.color;

                // 앰비언트
                float3 ambient = SampleSH(normalWS);

                // 최종 색상: 정점 색상(Gradient) × 베이스 컬러 × 라이팅
                float3 baseColor = input.color.rgb * _BaseColor.rgb;
                float3 finalColor = baseColor * (diffuse + ambient) + specular + rim;
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // ===== Shadow Caster Pass =====
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vertShadow
            #pragma fragment fragShadow

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vertShadow(Attributes input)
            {
                Varyings output;
                float3 worldPos, normalWS;
                ComputeRopeVertex(input.uv, worldPos, normalWS);
                output.positionCS = TransformWorldToHClip(worldPos);

                #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            half4 fragShadow(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }

        // ===== Depth Only Pass =====
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vertDepth
            #pragma fragment fragDepth

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vertDepth(Attributes input)
            {
                Varyings output;
                float3 worldPos, normalWS;
                ComputeRopeVertex(input.uv, worldPos, normalWS);
                output.positionCS = TransformWorldToHClip(worldPos);
                return output;
            }

            half4 fragDepth(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
