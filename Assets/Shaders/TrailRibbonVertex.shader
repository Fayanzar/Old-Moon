Shader "Custom/TrailRibbonVertex"
{
    Properties
    {
        _Color ("Trail Color", Color) = (0.4, 0.8, 1.0, 1.0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            // Each element: xyz = world-space position, w = age in seconds.
            // Laid out as a flat ring buffer; the vertex shader does the index
            // unwrapping itself, same as the GS version.
            StructuredBuffer<float4> _Points;
            UNITY_INSTANCING_BUFFER_START(Props)
                uint   _Capacity;
                int   _Head;
                int   _Count;
                float _PixelWidth;
                float _FadeTime;
                float4 _Color;
            UNITY_INSTANCING_BUFFER_END(Props)

            // Corner table for two triangles making one quad.
            // Each row is (pointOffset, sign): pointOffset selects p0 or p1 of
            // the segment, sign picks which side of the ribbon (+1 or -1).
            //
            //   p0 ---- p1
            //   |  \     |
            //   |    \   |
            //   p0'--- p1'
            //
            // Triangle 0: (p0, -1), (p0, +1), (p1, +1)
            // Triangle 1: (p0, -1), (p1, +1), (p1, -1)
            //
            // Stored as int2(pointOffset, side) where side is 0 or 1 (mapped to -1/+1).
            static const int2 CORNERS[6] = {
                int2(0, 0),  // tri 0, vert 0: p0, side -1
                int2(0, 1),  // tri 0, vert 1: p0, side +1
                int2(1, 1),  // tri 0, vert 2: p1, side +1
                int2(0, 0),  // tri 1, vert 0: p0, side -1
                int2(1, 1),  // tri 1, vert 1: p1, side +1
                int2(1, 0),  // tri 1, vert 2: p1, side -1
            };

            struct appdata
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID // Required for instancing setup
            };

            struct v2f
            {
                float4 clipPos : SV_POSITION;
                float  age     : TEXCOORD0;
                float2 uv_w    : TEXCOORD1;
                float  clipW   : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Identical to the GS version: converts pixelWidth -> correct world-space
            // half-width at this vertex's camera distance, so the ribbon appears the
            // same number of pixels wide regardless of how far away it is.
            float3 RibbonOffset(float3 worldPos, float3 segDir, float side, float pixelWidth)
            {
                float3 toCam = _WorldSpaceCameraPos - worldPos;
                float  dist  = length(toCam);

                // Perpendicular to both the segment and the view ray = ribbon facing camera
                float3 widthDir = normalize(cross(segDir, toCam / max(dist, 1e-5)));

                // World units per pixel at this distance using the camera's
                // projection matrix (encodes cot(fov/2)), and viewport height.
                float worldPerPixel = (2.0 * dist) / (_ScreenParams.y * unity_CameraProjection._m11);

                return widthDir * (side * 0.5 * pixelWidth * worldPerPixel);
            }

            // Unwrap ring-buffer index: logical index 0 = oldest live point.
            int RingIndex(int logicalIndex, int head, int count, uint capacity)
            {
                int i = (head - count + logicalIndex) % capacity;
                if (i < 0) i += capacity;
                return i;
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                uint capacity    = UNITY_ACCESS_INSTANCED_PROP(Props, _Capacity);
                int head         = UNITY_ACCESS_INSTANCED_PROP(Props, _Head);
                int count        = UNITY_ACCESS_INSTANCED_PROP(Props, _Count);
                float pixelWidth = UNITY_ACCESS_INSTANCED_PROP(Props, _PixelWidth);
                float fadeTime   = UNITY_ACCESS_INSTANCED_PROP(Props, _FadeTime);

                // Which segment, and which corner of its quad?
                int segIdx    = (int)v.vertexID / 6u;
                int cornerIdx = (int)v.vertexID % 6u;

                int2 corner = CORNERS[cornerIdx];
                int  ptOffset = corner.x;           // 0 = start of segment, 1 = end
                float side    = corner.y ? 1.0 : -1.0; // which ribbon edge

                // Fetch the two world-space points that bound this segment.
                float4 rawP0 = _Points[RingIndex(segIdx, head, count, capacity)];
                float4 rawP1 = _Points[RingIndex(segIdx + 1, head, count, capacity)];

                float3 p0 = rawP0.xyz;
                float3 p1 = rawP1.xyz;

                // The point THIS vertex belongs to.
                float3 myPos = ptOffset == 0 ? p0 : p1;
                float  myAge = ptOffset == 0 ? rawP0.w : rawP1.w;

                // Segment direction (same for both ends; avoids recomputing per corner).
                float3 segDir = p1 - p0;
                float  segLen = length(segDir);

                // Skip degenerate segments (same point twice) by collapsing to clip origin.
                // They'll produce zero-area triangles that the rasterizer discards.
                if (segLen < 1e-6)
                {
                    o.clipPos = float4(0, 0, 0, 1);
                    o.age = 1e9; // force fully faded -> invisible
                    return o;
                }

                segDir /= segLen;

                float3 worldPos = myPos + RibbonOffset(myPos, segDir, side, pixelWidth);

                float2 uv = float2(side, 1.0 * segIdx / count);

                o.clipPos = UnityWorldToClipPos(worldPos);
                o.uv_w    = uv * o.clipPos.w;
                o.clipW   = o.clipPos.w;
                o.age     = myAge;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float fadeTime = UNITY_ACCESS_INSTANCED_PROP(Props, _FadeTime);
                float4 color   = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);

                float fade = saturate(1.0 - (i.age / fadeTime));
                fixed4 col = color;
                float2 uv = i.uv_w / i.clipW;
                float grad = smoothstep(0.0, 0.8, 1 - abs(uv.x));
                return half4(color.rgb, color.a * fade * grad);
            }

            ENDCG
        }
    }
}
