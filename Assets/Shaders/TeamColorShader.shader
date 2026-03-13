Shader "Custom/TeamColorShader"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _TeamColor ("Team Color", Color) = (1,1,1,1)
        _HueThreshold ("Hue Threshold", Range(0.01, 0.5)) = 0.1
        _SaturationMin ("Saturation Minimum", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // _MainTex_ST hors CBUFFER : compatible SRP Batcher 2D + supporte flipX/flipY des sprites
            float4 _MainTex_ST;

            CBUFFER_START(UnityPerMaterial)
                float4 _TeamColor;
                float _HueThreshold;
                float _SaturationMin;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            // // Convertit RGB en HSV
            float3 RGBtoHSV(float3 rgb)
            {
                float maxC = max(rgb.r, max(rgb.g, rgb.b));
                float minC = min(rgb.r, min(rgb.g, rgb.b));
                float delta = maxC - minC;

                float h = 0;
                float s = (maxC > 0) ? delta / maxC : 0;
                float v = maxC;

                if (delta > 0)
                {
                    if (maxC == rgb.r)
                        h = (rgb.g - rgb.b) / delta;
                    else if (maxC == rgb.g)
                        h = 2 + (rgb.b - rgb.r) / delta;
                    else
                        h = 4 + (rgb.r - rgb.g) / delta;

                    h /= 6;
                    if (h < 0) h += 1;
                }
                return float3(h, s, v);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float4 pixel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float3 hsv = RGBtoHSV(pixel.rgb);

                // // Teinte du bleu pur #0000FF = 0.6667 en HSV
                float blueHue = 0.6667;
                float hueDist = abs(hsv.x - blueHue);

                // // Détecte si teinte bleue ET suffisamment saturée (évite les gris/blancs)
                bool isTeamColor = hueDist < _HueThreshold && hsv.y > _SaturationMin;

                if (isTeamColor)
                {
                    // // Préserve la luminosité (V) originale → garde les nuances d'ombre/lumière
                    float3 teamHSV = RGBtoHSV(_TeamColor.rgb);
                    float3 resultHSV = float3(teamHSV.x, teamHSV.y, hsv.z);
                    
                    // // Reconvertit HSV en RGB
                    float3 rgb;
                    float h = resultHSV.x * 6;
                    int i = (int)h;
                    float f = h - i;
                    float p = resultHSV.z * (1 - resultHSV.y);
                    float q = resultHSV.z * (1 - resultHSV.y * f);
                    float t = resultHSV.z * (1 - resultHSV.y * (1 - f));

                    if (i == 0) rgb = float3(resultHSV.z, t, p);
                    else if (i == 1) rgb = float3(q, resultHSV.z, p);
                    else if (i == 2) rgb = float3(p, resultHSV.z, t);
                    else if (i == 3) rgb = float3(p, q, resultHSV.z);
                    else if (i == 4) rgb = float3(t, p, resultHSV.z);
                    else rgb = float3(resultHSV.z, p, q);

                    return float4(rgb, pixel.a);
                }

                return pixel * IN.color;
            }
            ENDHLSL
        }
    }
}
