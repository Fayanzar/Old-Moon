Shader "Custom/UI/Circle"
{
    Properties
    {
        _Color         ("Color",           Color)  = (1,1,1,1)
        _Thickness     ("Thickness",       Range(0, 0.5)) = 0.05
        _Softness      ("Edge Softness",   Range(0, 0.1)) = 0.01

        // Required by Unity UI — do not remove
        [HideInInspector] _MainTex        ("Main Tex",    2D)    = "white" {}
        [HideInInspector] _StencilComp    ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil        ("Stencil ID",         Float) = 0
        [HideInInspector] _StencilOp      ("Stencil Operation",  Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask  ("Stencil Read Mask",  Float) = 255
        [HideInInspector] _ColorMask      ("Color Mask",  Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        // Unity UI stencil — required for correct masking with UI Mask components
        Stencil
        {
            Ref        [_Stencil]
            Comp       [_StencilComp]
            Pass       [_StencilOp]
            ReadMask   [_StencilReadMask]
            WriteMask  [_StencilWriteMask]
        }

        Cull     Off
        Lighting Off
        ZWrite   Off
        ZTest    [unity_GUIZTestMode]
        Blend    SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            float4 _Color;
            float  _Thickness;
            float  _Softness;
            float4 _ClipRect;       // provided by UnityUI.cginc / CanvasRenderer

            sampler2D _MainTex;
            float4    _MainTex_ST;

            struct vertexInput
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct vertexOutput
            {
                float4 pos      : SV_POSITION;
                float4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                float4 worldPos : TEXCOORD1;    // needed for RectMask2D clipping
            };

            vertexOutput vert(vertexInput IN)
            {
                vertexOutput OUT;
                OUT.pos      = UnityObjectToClipPos(IN.vertex);
                OUT.worldPos = IN.vertex;
                OUT.uv       = TRANSFORM_TEX(IN.texcoord, _MainTex);
                // Multiply by vertex color so the Image component's color tint works
                OUT.color    = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(vertexOutput IN) : SV_Target
            {
                // Remap UV from [0,1] to [-1,1] so the centre is (0,0)
                float2 uv = IN.uv * 2.0 - 1.0;

                // Distance from centre
                float dist = length(uv);

                // Outer edge: circle boundary at radius 1
                // Inner edge: hollow centre, radius = 1 - thickness*2
                float outerEdge = 1.0 - _Softness;
                float innerEdge = 1.0 - _Thickness * 2.0;

                // Smooth ring: full alpha between inner and outer edges
                float alpha = smoothstep(1.0, outerEdge, dist)           // fade outer rim
                            * smoothstep(innerEdge - _Softness,
                                         innerEdge, dist);               // fade inner rim

                fixed4 col = IN.color;
                col.a *= alpha;

                // Unity UI RectMask2D clipping
                col.a *= UnityGet2DClipping(IN.worldPos.xy, _ClipRect);

                // Discard fully transparent pixels (good practice for UI)
                clip(col.a - 0.001);

                return col;
            }

            ENDCG
        }
    }
}
