// Shader outline pixel art pour sprites 2D
// Appliqué via un SpriteRenderer enfant dédié à l'outline
// Principe : dilate le sprite dans 8 directions, dessine la couleur d'outline
//            là où le sprite dilaté dépasse le sprite original (= contour)
Shader "Custom/SpriteOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineSize  ("Outline Size (px)", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Transparent"
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType"    = "Plane"
        }

        Cull   Off
        ZWrite Off
        Blend  SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // Hors CBUFFER : le SRP Batcher 2D interdit _ST et _TexelSize dans UnityPerMaterial
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4  _OutlineColor;
                float  _OutlineSize;
            CBUFFER_END

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // Alpha du sprite original à ce pixel
                half alphaCenter = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;

                // Si le pixel original est opaque → pas d'outline ici (inside du sprite)
                if (alphaCenter > 0.5h)
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);

                // Taille d'un texel en UV
                float2 texel = _MainTex_TexelSize.xy * _OutlineSize;

                // Échantillonnage 8 directions → cherche si un voisin est opaque
                float2 offsets[8];
                offsets[0] = float2( texel.x,  0.0     );
                offsets[1] = float2(-texel.x,  0.0     );
                offsets[2] = float2( 0.0,       texel.y);
                offsets[3] = float2( 0.0,      -texel.y);
                offsets[4] = float2( texel.x,   texel.y);
                offsets[5] = float2(-texel.x,   texel.y);
                offsets[6] = float2( texel.x,  -texel.y);
                offsets[7] = float2(-texel.x,  -texel.y);

                float maxNeighborAlpha = 0.0;
                for (int i = 0; i < 8; i++)
                {
                    float neighborAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + offsets[i]).a;
                    maxNeighborAlpha = max(maxNeighborAlpha, neighborAlpha);
                }

                // Un voisin est dans le sprite → pixel de contour
                if (maxNeighborAlpha > 0.5)
                    return _OutlineColor;

                // Aucun voisin opaque → transparent
                return half4(0.0h, 0.0h, 0.0h, 0.0h);
            }
            ENDHLSL
        }
    }
}
