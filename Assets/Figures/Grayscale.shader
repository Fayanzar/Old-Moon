Shader "Hidden/Custom/Grayscale"
{
    HLSLINCLUDE

        #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"

        TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
        float _Blend;

        float random(float2 p)
        {
            float2 K1 = float2(
                23.14069263277926,
                2.665144142690225
            );
            return frac( cos( dot(p,K1) ) * 12345.6789 );
        }

        float3x3 AngleAxis3x3(float angle, float3 axis)
        {
            float c, s;
            sincos(angle, s, c);

            float t = 1 - c;
            float x = axis.x;
            float y = axis.y;
            float z = axis.z;

            return float3x3(
                t * x * x + c,      t * x * y - s * z,  t * x * z + s * y,
                t * x * y + s * z,  t * y * y + c,      t * y * z - s * x,
                t * x * z - s * y,  t * y * z + s * x,  t * z * z + c
            );
        }

        float distance(float4 plane, float3 p)
        {
            return abs(plane.x * p.x + plane.y * p.y + plane.z * p.z + plane.w) /
                     sqrt(plane.x * plane.x + plane.y * plane.y + plane.z * plane.z);
        }

        float4 Frag(VaryingsDefault ii) : SV_Target
        {
            //float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord);
            float4 color = float4(1,1,1,0);
            float w, h;
            _MainTex.GetDimensions(w, h);
            float3 axis = normalize(float3(0, 1, 0.7));
            float angle = 3.14/4;

            float c, s;
            sincos(angle, s, c);

            float t = 1 - c;
            float x = axis.x;
            float y = axis.y;
            float z = axis.z;

            float3x3 matrix_r = AngleAxis3x3(angle, axis);
            float3 coords = float3(w * ii.texcoord.x, h * ii.texcoord.y, 50);

            float3 new_coords = mul(matrix_r, coords);

            float4 colors[10];
            int k = 0;

            for (uint i = 0; i < 10; i++)
            {
                colors[i] = float4(1,1,1,0);
                for (int j = 0; j < 10; j++)
                {
                    float r;
                    switch (i % 4)
                    {
                        case 0:
                            r = (random(float2(k,0)) - 0.5) * 30 + (k - 50) * (w + h) / 45;
                            if (distance(float4(1, 1, 1, r), float3(new_coords.x, new_coords.y, new_coords.z)) < 3)
                                colors[i] = float4(0, 0, 0, 1);
                            if (distance(float4(1, 1, 1, -r), float3(-new_coords.x, new_coords.y, new_coords.z)) < 3)
                                colors[i] = float4(0.3, 0.3, 0.3, 1);
                                break;
                        case 1:
                            r = (random(float2(1.1 * k,0)) - 0.5) * 30 + (k - 75) * (w + h) / 45;
                            if (distance(float4(1, 1, 1, r), float3(new_coords.x, new_coords.y, -new_coords.z)) < 3)
                                colors[i] = float4(0, 0, 0, 1);
                            if (distance(float4(1, 1, 1, -r), float3(-new_coords.x, new_coords.y, -new_coords.z)) < 3)
                                colors[i] = float4(0.3, 0.3, 0.3, 1);
                                break;
                        case 3:
                            r = (random(float2(2.3*k,0)) - 0.5) * 30 + (k - 50) * (w + h) / 45;
                            if (distance(float4(1, 1, 1, r), float3(new_coords.x, -new_coords.y, new_coords.z)) < 3)
                                colors[i] = float4(0, 0, 0, 1);
                            if (distance(float4(1, 1, 1, -r), float3(-new_coords.x, -new_coords.y, new_coords.z)) < 3)
                                colors[i] = float4(0.3, 0.3, 0.3, 1);
                                break;
                        case 4:
                            r = (random(float2(3.7*k,0)) - 0.5) * 30 + (k - 50) * (w + h) / 45;
                            if (distance(float4(1, 1, 1, r), float3(new_coords.x, -new_coords.y, -new_coords.z)) < 3)
                                colors[i] = float4(0, 0, 0, 1);
                            if (distance(float4(1, 1, 1, -r), float3(-new_coords.x, -new_coords.y, -new_coords.z)) < 3)
                                colors[i] = float4(0.3, 0.3, 0.3, 1);
                            break;
                    }
                    k++;
                }
                if (color.a == 0)
                    color = colors[i];
            }

            return color;
        }

    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM

                #pragma vertex VertDefault
                #pragma fragment Frag

            ENDHLSL
        }
    }
}
