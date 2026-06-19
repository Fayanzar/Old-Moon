// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/SphereShadow" {
   Properties {
      _Color ("Diffuse Material Color", Color) = (1,1,1,1)
      _MainTex ("Base (RGB)", 2D) = "white" {}
      _BumpMap ("Normal Map", 2D) = "normal" {}
      _SpecColor ("Specular Material Color", Color) = (1,1,1,1)
      _Shininess ("Shininess", float) = 10
      _AtmosphereColor ("Atmosphere Color", Color) = (0.15, 0.35, 0.9, 1)
      _SunsetColor ("Sunset Color", Color) = (0.8, 0.25, 0.05, 1)
      [PowerSlider(4)] _AtmosphereExponent ("Atmosphere Exponent", Range(0.25, 6)) = 3
      [PowerSlider(4)] _SunsetExponent ("Sunset Exponent", Range(0.25, 6)) = 6
   }
   SubShader {
      Blend SrcAlpha OneMinusSrcAlpha
      Pass {
         CGPROGRAM

         #pragma vertex vert
         #pragma fragment frag

         #pragma target 3.0

         #include "UnityCG.cginc"
         uniform float4 _LightColors[4];
         uniform float4 _LightPositions[4];
         uniform float _LightRadii[4];
         uniform int _LightNumber;
         // color of light source (from "Lighting.cginc")

         // User-specified properties
         uniform float4 _Color;
         uniform float4 _SpecColor;
         uniform float _Shininess;
         uniform float4 _SpherePositions[1024];
         uniform float _SphereRadii[1024];
         uniform int _SphereNumber;

         uniform sampler2D _MainTex;
         uniform float4 _MainTex_ST;

         uniform sampler2D _BumpMap;
         uniform float4 _BumpMap_ST;

         float3 _AtmosphereColor;
         float3 _SunsetColor;
         float _AtmosphereExponent;
         float _SunsetExponent;

         struct vertexInput {
            float4 vertex : POSITION;
            float2 texcoord : TEXCOORD0;
            float3 normal : NORMAL;
            float4 tangent : TANGENT;
         };
         struct vertexOutput {
            float4 pos : SV_POSITION;
            float4 posWorld : TEXCOORD0;
            float3 normalDir : TEXCOORD1;
            float2 texMain : TEXCOORD2;
            float2 texBump : TEXCOORD3;

            float3 tangentDir : TEXCOORD4;
            float3 binormalDir : TEXCOORD5;
         };

         vertexOutput vert(vertexInput input)
         {
            vertexOutput output;

            float4x4 modelMatrix = unity_ObjectToWorld;
            float4x4 modelMatrixInverse = unity_WorldToObject;

            output.tangentDir = normalize(
               mul(modelMatrix, float4(input.tangent.xyz, 0.0)).xyz);
            output.normalDir = normalize(
               mul(float4(input.normal, 0.0), modelMatrixInverse).xyz);
            output.binormalDir = normalize(
              cross(output.normalDir, output.tangentDir) * input.tangent.w);

            output.pos = UnityObjectToClipPos(input.vertex);
            output.posWorld = mul(modelMatrix, input.vertex);
            output.texMain = TRANSFORM_TEX(input.texcoord, _MainTex);
            output.texBump = TRANSFORM_TEX(input.texcoord, _BumpMap);
            return output;
         }

         float4 frag(vertexOutput input) : COLOR
         {
            float4 encodedNormal = tex2D(_BumpMap, input.texBump);
            float3 localCoords = float3(2.0 * encodedNormal.a - 1.0,
               2.0 * encodedNormal.g - 1.0, 0.0);
            localCoords.z = sqrt(1.0 - dot(localCoords, localCoords));

            float3x3 local2WorldTranspose = float3x3(
               input.tangentDir,
               input.binormalDir,
               input.normalDir);
            float3 normalDirection = normalize(mul(localCoords, local2WorldTranspose));

            float3 viewDirection = normalize(_WorldSpaceCameraPos - input.posWorld.xyz);
            float attenuation;
            float3 ambientLighting = UNITY_LIGHTMODEL_AMBIENT.rgb * _Color.rgb;
            float3 diffuseReflection = float3(0, 0, 0);
            float3 specularReflection = float3(0, 0, 0);

            float4 texCol = tex2D(_MainTex, input.texMain);

            float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - input.posWorld);

            float fresnel = dot(input.normalDir, viewDir);
            fresnel = saturate(1 - fresnel);
            float rayleigh = pow(fresnel, _AtmosphereExponent);
            float sunset = pow(fresnel, _SunsetExponent);

            float rayleighIntensity = 0.7;
            float sunsetIntensity   = 0.4;

            float3 fresnelLight = float3(0, 0, 0);

            for (int li = 0; li < _LightNumber; li++) {
               float3 lightDirection = _LightPositions[li].xyz - input.posWorld.xyz;
               float lightDistance = length(lightDirection);
                  //attenuation = 1.0 / lightDistance; // linear attenuation
               attenuation = 1.0;
               lightDirection = lightDirection / lightDistance;

               // computation of level of shadowing w
               float3 oneMinusW = 1.0;

               for (int i = 0; i < _SphereNumber; i++)
               {
                  float3 sphereDirection = _SpherePositions[i].xyz - input.posWorld.xyz;
                  float sphereDistance = length(sphereDirection);
                  sphereDirection = sphereDirection / sphereDistance;

                  float cosAngle = dot(lightDirection, sphereDirection);
                  if (cosAngle < 0.0) continue;
                  if (sphereDistance > lightDistance) continue;

                  float d = lightDistance * (asin(min(1.0, length(cross(lightDirection, sphereDirection)))) - asin(min(1.0, _SphereRadii[i] / sphereDistance)));
                  float w = smoothstep(-1.0, 1.0, -d / _LightRadii[li]);
                  w = w * smoothstep(0.0, 0.2, dot(lightDirection, sphereDirection));
                  if (0.0 != _WorldSpaceLightPos0.w) // point light source?
                  {
                     w = w * smoothstep(0.0, _SphereRadii[i], lightDistance - sphereDistance);
                  }
                  oneMinusW = oneMinusW * (1 - w);
               }
               float NdotL = dot(normalDirection, lightDirection);
               float3 diffuseReflectionI = saturate(NdotL);

               float3 specularReflectionI = float3(0.0, 0.0, 0.0);
               float3 normal = normalize(input.normalDir);
               float3 halfDir = normalize(lightDirection + viewDir);
               float specAngle = saturate(dot(halfDir, normal));
               specularReflectionI = pow(specAngle, _Shininess);

               float litFresnel = smoothstep(-0.2, 0.4, NdotL);
               fresnelLight += litFresnel * _LightColors[li].rgb;

               diffuseReflection += oneMinusW * diffuseReflectionI * _LightColors[li].rgb;
               specularReflection += oneMinusW * specularReflectionI * _LightColors[li].rgb;
            }

            float3 fresnelColor = (rayleigh * _AtmosphereColor * rayleighIntensity + sunset * _SunsetColor * sunsetIntensity) * fresnelLight;

            diffuseReflection *= attenuation * _Color.rgb * texCol.rgb;
            specularReflection *= attenuation * _SpecColor.rgb;

            return fixed4(ambientLighting + fresnelColor + (diffuseReflection + specularReflection), 1.0);
         }

         ENDCG
      }
   }
   Fallback "Specular"
}
