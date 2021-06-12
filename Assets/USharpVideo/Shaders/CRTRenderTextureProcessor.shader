Shader "Merlin/World/Render Texture Processor"
{
    Properties
    {
        [HDR]_Color("Color", Color) = (1, 1, 1, 1)
        _SourceTexture("Source Texture", 2D) = "black"
        _IsAVPro("Is AV Pro", Int) = 0
        _TargetAspectRatio("Target Aspect Ratio", Float) = 1.77777777
    }
        SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            Name "ResizeInput"

            CGPROGRAM
            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "UnityCustomRenderTexture.cginc"

            float4 _Color;
            sampler2D_float _SourceTexture; float4 _SourceTexture_TexelSize; float4 _SourceTexture_ST;
            int _IsAVPro;
            float _TargetAspectRatio;

            half3 VideoEmission(float2 uv)
            {
                uv = TRANSFORM_TEX(uv, _SourceTexture);

                float2 emissionRes = _SourceTexture_TexelSize.zw;

                float currentAspectRatio = emissionRes.x / emissionRes.y;

                float visibility = 1.0;

                // If the aspect ratio does not match the target ratio, then we fit the UVs to maintain the aspect ratio while fitting the range 0-1
                if (abs(currentAspectRatio - _TargetAspectRatio) > 0.001)
                {
                    float2 normalizedVideoRes = float2(emissionRes.x / _TargetAspectRatio, emissionRes.y);
                    float2 correctiveScale;

                    // Find which axis is greater, we will clamp to that
                    if (normalizedVideoRes.x > normalizedVideoRes.y)
                        correctiveScale = float2(1, normalizedVideoRes.y / normalizedVideoRes.x);
                    else
                        correctiveScale = float2(normalizedVideoRes.x / normalizedVideoRes.y, 1);

                    uv = ((uv - 0.5) / correctiveScale) + 0.5;

                    // Antialiasing on UV clipping
                    //float2 uvPadding = 0;
                    //float2 uvfwidth = fwidth(uv.xy);
                    //float2 maxFactor = smoothstep(uvfwidth + uvPadding + 1, uvPadding + 1, uv.xy);
                    //float2 minFactor = smoothstep(-uvfwidth - uvPadding, -uvPadding, uv.xy);

                    //visibility = maxFactor.x * maxFactor.y * minFactor.x * minFactor.y;
                }

                if (any(uv <= 0) || any(uv >= 1))
                    return float3(0, 0, 0);

                float3 texColor = tex2D(_SourceTexture, _IsAVPro ? float2(uv.x, 1 - uv.y) : uv).rgb;

                if (_IsAVPro)
                    texColor = pow(texColor, 2.2f);

                return texColor * _Color.rgb * visibility;
            }

            float4 frag (v2f_customrendertexture i) : SV_Target
            {
                return float4(VideoEmission(i.globalTexcoord), 1);
            }
            ENDCG
        }
    }
}
