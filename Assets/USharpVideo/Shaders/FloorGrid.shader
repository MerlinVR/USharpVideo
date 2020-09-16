Shader "Merlin/World/FloorGrid"
{
    Properties
    {
        _Tiling("Tiling", Float) = 1.0
        _GridThickness("Grid Thickness", Range(0, 1)) = 0.1
        _FadeDistance("Fade Distance", Float) = 1.0
        [HDR]_GridColor("Grid Color", Color) = (1, 1, 1, 1)
	}
	SubShader
	{
		Tags { "RenderType"="Opaque"
               "Queue"="Transparent" }
		LOD 100

        Blend SrcAlpha One
        BlendOp Add
        Cull Off
        ZWrite Off

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
			};
			
            float _Tiling;
            float _GridThickness;
            float _FadeDistance;
            float4 _GridColor;

            float3 WorldHeadPosition()
            {
            #if defined(USING_STEREO_MATRICES)
                return (unity_StereoWorldSpaceCameraPos[0] + unity_StereoWorldSpaceCameraPos[1]) * 0.5;
            #else
                return _WorldSpaceCameraPos;
            #endif
            }

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

				return o;
			}
			
			float4 frag (v2f i) : SV_Target
			{
                float2 gridFrac = frac(i.worldPos.xz * _Tiling + _GridThickness * 0.5);
                float2 gridDistance = 1 - gridFrac;

                float gridAmount = max(gridDistance.x, gridDistance.y) > (1 - _GridThickness) ? 1 : 0;

                float distanceFromHead = length(WorldHeadPosition().xz - i.worldPos.xz);

                float fadeAmount = saturate(_FadeDistance - distanceFromHead);
                fadeAmount *= fadeAmount * fadeAmount;
                fadeAmount = smoothstep(1, .1, 1 - fadeAmount);

                return float4(1, 1, 1, gridAmount * fadeAmount) * _GridColor;
			}
			ENDCG
		}
	}
}
