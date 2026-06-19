Shader "Custom/PixelStarBillboard"
{
    Properties
    {
        [HDR] _Color    ("Star Color",          Color) = (1,1,1,1)
        _CoreBrightness ("Core Brightness",      Range(0, 10)) = 3.0
        _CoreSharpness  ("Core Sharpness",       Range(0.5, 8)) = 2.5
        _GlowFalloff    ("Glow Falloff",         Range(0.5, 8)) = 1.0
        _Intensity      ("Overall Intensity",    Range(0, 10)) = 1.0

        // Desired on-screen size in pixels — used by the C# side, kept here
        // for reference / inspector visibility only
        _TargetPixelSize ("Target Pixel Size (info only)", Float) = 4.0
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }

        Cull     Off
        ZWrite   Off
        ZTest    LEqual
        Blend    One One          // additive — correct for light sources, avoids
                                   // dark halos when billboards overlap or sit
                                   // in front of bright backgrounds

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   2.0

            #include "UnityCG.cginc"

            float4 _Color;
            float  _CoreBrightness;
            float  _CoreSharpness;
            float  _GlowFalloff;
            float  _Intensity;

            struct vertexInput
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct vertexOutput
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            // ---------------------------------------------------------------
            // Vertex shader: spherical billboarding.
            // Rebuilds the quad facing the camera directly in view space,
            // which keeps it pixel-stable regardless of object rotation
            // and avoids the "billboard swimming" artifact you get from
            // naive LookAt-style billboarding.
            // ---------------------------------------------------------------
            vertexOutput vert(vertexInput IN)
            {
                vertexOutput OUT;

                // Object's pivot in view space
                float3 centerVS = mul(UNITY_MATRIX_MV, float4(0, 0, 0, 1)).xyz;

                // Use local vertex.xy as the quad offset (assumes a unit quad
                // centred on the pivot, e.g. corners at ±0.5).
                // Object scale still applies via the object->world matrix,
                // so resizing the GameObject scales the billboard normally.
                float3 worldScale = float3(
                    length(unity_ObjectToWorld._m00_m10_m20),
                    length(unity_ObjectToWorld._m01_m11_m21),
                    length(unity_ObjectToWorld._m02_m12_m22));

                float2 offset = IN.vertex.xy * worldScale.xy;

                float3 posVS = centerVS + float3(offset, 0.0);
                OUT.pos = mul(UNITY_MATRIX_P, float4(posVS, 1.0));

                OUT.uv = IN.texcoord;
                return OUT;
            }

            // ---------------------------------------------------------------
            // Fragment shader: soft radial falloff, no hard edges.
            // The gradient itself does the antialiasing, so the rendered
            // value is stable regardless of how the quad's edge falls
            // between pixels — this is what kills the flicker.
            // ---------------------------------------------------------------
            fixed4 frag(vertexOutput IN) : SV_Target
            {
                float2 uv   = IN.uv * 2.0 - 1.0;   // [-1, 1], centre at origin
                float  dist = length(uv);

                // Soft outer glow — wide, gentle falloff
                float glow = saturate(1.0 - dist);
                glow = pow(glow, _GlowFalloff);

                // Bright core — tight, sharp falloff, this is what bloom grabs
                float core = saturate(1.0 - dist * 1.6);
                core = pow(core, _CoreSharpness);

                float3 color = _Color.rgb *
                    ((glow * 1.0) + (core * _CoreBrightness));

                color *= _Intensity;

                // Alpha unused in additive blending but kept for consistency
                // / for use if you switch to alpha blending for some bodies
                float alpha = saturate(glow + core);

                return fixed4(color, alpha);
            }

            ENDCG
        }
    }
}
