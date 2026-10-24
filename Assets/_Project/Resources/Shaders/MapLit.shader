// The map's chunks, lit by URP 2D lights like any sprite. Where the painter tagged a pixel with a
// ground material (alpha below 1: 255 - id), the pixel's colour is only light and shade (grey 128 =
// as lit), and the sharp baked material, repeating in world space, supplies the surface.
Shader "OSistema/MapLit"
{
    Properties
    {
        _MainTex("Painted map", 2D) = "white" {}
        _Mat1("Asphalt", 2D) = "grey" {}
        _Mat2("Basoli", 2D) = "grey" {}
        _Mat3("Kerb east-west", 2D) = "grey" {}
        _Mat4("Kerb north-south", 2D) = "grey" {}
        _Mat5("Slabs", 2D) = "grey" {}
        _Mat6("Coppi", 2D) = "grey" {}
        _Mat7("Tar", 2D) = "grey" {}
        _Mat8("Cotto", 2D) = "grey" {}
        _Mat9("Cement tiles", 2D) = "grey" {}
        _Mat10("Plaster", 2D) = "grey" {}
        _Mat11("Grass", 2D) = "grey" {}
        _Mat12("Earth", 2D) = "grey" {}
        _Mat13("Floor tiles", 2D) = "grey" {}
        _Mat14("Marble", 2D) = "grey" {}
        _Mat15("Vicolo north-south", 2D) = "grey" {}
        _Mat16("Vicolo east-west", 2D) = "grey" {}
        _Mat17("Piazza", 2D) = "grey" {}
        _Mat18("Marciapiede", 2D) = "grey" {}
        _Mat19("Graniglia", 2D) = "grey" {}
        _Mat20("Basoli wet", 2D) = "grey" {}
        _Wet("Rain on the stone", Range(0, 1)) = 0
        _Period("Cells per repeat", Float) = 8
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half2 lightingUV : TEXCOORD1;
                float2 world : TEXCOORD2;
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex); float4 _MainTex_TexelSize;
            TEXTURE2D(_Mat1); SAMPLER(sampler_Mat1);
            TEXTURE2D(_Mat2);
            TEXTURE2D(_Mat3);
            TEXTURE2D(_Mat4);
            TEXTURE2D(_Mat5);
            TEXTURE2D(_Mat6);
            TEXTURE2D(_Mat7);
            TEXTURE2D(_Mat8);
            TEXTURE2D(_Mat9);
            TEXTURE2D(_Mat10);
            TEXTURE2D(_Mat11);
            TEXTURE2D(_Mat12);
            TEXTURE2D(_Mat13);
            TEXTURE2D(_Mat14);
            TEXTURE2D(_Mat15);
            TEXTURE2D(_Mat16);
            TEXTURE2D(_Mat17);
            TEXTURE2D(_Mat18);
            TEXTURE2D(_Mat19);
            TEXTURE2D(_Mat20);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Period;
                float _Wet;
            CBUFFER_END

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv = v.uv;
                o.world = TransformObjectToWorld(v.positionOS).xy;
                o.lightingUV = half2(ComputeScreenPos(o.positionCS / o.positionCS.w).xy);
                o.color = v.color * _Color;
                return o;
            }

            half3 Ground(int id, float2 uv, float2 dx, float2 dy)
            {
                // One sampler for all (same settings: repeat, trilinear), one result.
                half3 r = half3(0.5, 0.5, 0.5);
                [branch] if (id == 1) r = SAMPLE_TEXTURE2D_GRAD(_Mat1, sampler_Mat1, uv, dx, dy).rgb;
                else if (id == 2) r = SAMPLE_TEXTURE2D_GRAD(_Mat2, sampler_Mat1, uv, dx, dy).rgb;
                else if (id == 3) r = SAMPLE_TEXTURE2D_GRAD(_Mat3, sampler_Mat1, uv, dx, dy).rgb;
                else if (id == 4) r = SAMPLE_TEXTURE2D_GRAD(_Mat4, sampler_Mat1, uv, dx, dy).rgb;
                else if (id == 5) r = SAMPLE_TEXTURE2D_GRAD(_Mat5, sampler_Mat1, uv, dx, dy).rgb;
                else if (id == 6) r = SAMPLE_TEXTURE2D_GRAD(_Mat6, sampler_Mat1, uv, dx, dy).rgb;
                else if (id == 7) r = SAMPLE_TEXTURE2D_GRAD(_Mat7, sampler_Mat1, uv, dx, dy).rgb;
                else if (id == 8) r = SAMPLE_TEXTURE2D_GRAD(_Mat8, sampler_Mat1, uv, dx, dy).rgb;
                else if (id == 9) r = SAMPLE_TEXTURE2D_GRAD(_Mat9, sampler_Mat1, uv, dx, dy).rgb;
                else if (id == 10) r = SAMPLE_TEXTURE2D_GRAD(_Mat10, sampler_Mat1, uv, dx, dy).rgb;
                else if (id == 11) r = SAMPLE_TEXTURE2D_GRAD(_Mat11, sampler_Mat1, uv, dx, dy).rgb;
                else if (id == 12) r = SAMPLE_TEXTURE2D_GRAD(_Mat12, sampler_Mat1, uv, dx, dy).rgb;
                else if (id == 13) r = SAMPLE_TEXTURE2D_GRAD(_Mat13, sampler_Mat1, uv, dx, dy).rgb;
                else if (id == 14) r = SAMPLE_TEXTURE2D_GRAD(_Mat14, sampler_Mat1, uv, dx, dy).rgb;
                else if (id == 15) r = SAMPLE_TEXTURE2D_GRAD(_Mat15, sampler_Mat1, uv, dx, dy).rgb;
                else if (id == 16) r = SAMPLE_TEXTURE2D_GRAD(_Mat16, sampler_Mat1, uv, dx, dy).rgb;
                else if (id == 17) r = SAMPLE_TEXTURE2D_GRAD(_Mat17, sampler_Mat1, uv, dx, dy).rgb;
                else if (id == 18) r = SAMPLE_TEXTURE2D_GRAD(_Mat18, sampler_Mat1, uv, dx, dy).rgb;
                else r = SAMPLE_TEXTURE2D_GRAD(_Mat19, sampler_Mat1, uv, dx, dy).rgb;
                // Rain: the lava stone goes dark and shines (the basoli and the vicoli).
                [branch] if (_Wet > 0.01 && (id == 2 || id == 15 || id == 16))
                    r = lerp(r, SAMPLE_TEXTURE2D_GRAD(_Mat20, sampler_Mat1, uv, dx, dy).rgb, _Wet);
                return r;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                // Derivatives outside any branch, for the materials' mip level.
                float2 guv = i.world / _Period;
                float2 gdx = ddx(guv), gdy = ddy(guv);
                int id = (int)round((1.0 - c.a) * 255.0);
                if (id > 0 && id < 24)
                {
                    // The light was written as sRGB grey (128 = as lit); in a linear project it
                    // arrives converted, so divide by what 128 becomes, not by a half.
                    #ifdef UNITY_COLORSPACE_GAMMA
                    c.rgb = Ground(id, guv, gdx, gdy) * c.rgb * 2.0;
                    #else
                    c.rgb = Ground(id, guv, gdx, gdy) * c.rgb / 0.21586;
                    #endif
                    c.a = 1.0;
                }
                c *= i.color;

                SurfaceData2D surfaceData;
                InputData2D inputData;
                InitializeSurfaceData(c.rgb, c.a, half4(1, 1, 1, 1), half3(0, 0, 1), surfaceData);
                InitializeInputData(i.uv, i.lightingUV, inputData);
                return CombinedShapeLightShared(surfaceData, inputData);
            }
            ENDHLSL
        }
    }
}
