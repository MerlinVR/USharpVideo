Shader "Merlin/World/CRTIdleVoronoi"
{
    Properties
    {
        [HDR]_Color("Color", Color) = (1, 1, 1, 1)
        _AnimationSpeed("Animation Speed", Float) = 0.1
        _NoiseScale("Noise Scale", Float) = 5.0
    }
        SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "UnityCustomRenderTexture.cginc"

            float4 _Color;
            float _AnimationSpeed;
            float _NoiseScale;

            float3 hash33(float3 p3)
            {
                p3 = frac(p3 * float3(.1031, .1030, .0973));
                p3 += dot(p3, p3.yxz + 33.33);
                return frac((p3.xxy + p3.yxx)*p3.zyx);
            }

            float voronoi(in float3 x)
            {
                float3 n = floor(x);
                float3 f = frac(x);

                //----------------------------------
                // first pass: regular voronoi
                //----------------------------------
                float3 mg, mr;

                float md = 8.0;
                {
                    for (int k = -1; k <= 1; k++)
                    for (int j = -1; j <= 1; j++)
                    for (int i = -1; i <= 1; i++)
                    {
                        float3 g = float3(float(i), float(j), float(k));
                        float3 o = hash33(n + g + 1);
                        //#ifdef ANIMATE
                        //o = 0.5 + 0.5*sin( _Time.x + 6.2831*o );
                    //      #else
                    //      o = 0.5 + 0.5*sin( 20 + 6.2831*o );
                    //      #endif	
                        float3 r = g + o - f;
                        float d = dot(r, r);

                        if (d < md)
                        {
                            md = d;
                            mr = r;
                            mg = g;
                        }
                    }
                }

                //----------------------------------
                // second pass: distance to borders
                //----------------------------------
                md = 8.0;
                for (int k = -1; k <= 1; k++)
                for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    float3 g = mg + float3(float(i),float(j),float(k));
                    float3 o = hash33(n + g + 1);
                    //#ifdef ANIMATE
                    //o = 0.5 + 0.5*sin( _Time.x+ 6.2831*o );
              //      #else
              //      o = 0.5 + 0.5*sin(20 + 6.2831*o);
              //      #endif	
                    float3 r = g + o - f;

                    if (dot(mr - r,mr - r) > 0.00001)
                    md = min(md, dot(0.5*(mr + r), normalize(r - mr)));
                }

                //return float4( md, mr );
                return md;
            }

            float4 frag (v2f_customrendertexture i) : SV_Target
            {
                float2 scaledUV = float2(i.globalTexcoord.x * (_CustomRenderTextureWidth / _CustomRenderTextureHeight), i.globalTexcoord.y) * _NoiseScale;
                float voronoiVal = voronoi(float3(scaledUV, _Time.y * _AnimationSpeed));
                
                float2 uvWidth = fwidth(scaledUV);
                return smoothstep(0.015, 0.01, voronoiVal) * _Color;
            }
            ENDCG
        }
    }
}
