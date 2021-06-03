// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)
// Copy of the Unity standard shader with tweakable realtime GI emissive seperate from the visible emissive

Shader "Merlin/World/Standard Video Emission"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Albedo", 2D) = "white" {}

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _Glossiness("Smoothness", Range(0.0, 1.0)) = 0.5
        _GlossMapScale("Smoothness Scale", Range(0.0, 1.0)) = 1.0
        [Enum(Metallic Alpha,0,Albedo Alpha,1)] _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0

        [Gamma] _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}

        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _GlossyReflections("Glossy Reflections", Float) = 1.0

        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}

        _Parallax("Height Scale", Range(0.005, 0.08)) = 0.02
        _ParallaxMap("Height Map", 2D) = "black" {}

        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}

        _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}

        _DetailMask("Detail Mask", 2D) = "white" {}

        _DetailAlbedoMap("Detail Albedo x2", 2D) = "grey" {}
        _DetailNormalMapScale("Scale", Float) = 1.0
        _DetailNormalMap("Normal Map", 2D) = "bump" {}

        [Enum(UV0,0,UV1,1)] _UVSec("UV Set for secondary textures", Float) = 0


            // Blending state
            [HideInInspector] _Mode("__mode", Float) = 0.0
            [HideInInspector] _SrcBlend("__src", Float) = 1.0
            [HideInInspector] _DstBlend("__dst", Float) = 0.0
            [HideInInspector] _ZWrite("__zw", Float) = 1.0

        _MetaPassEmissiveBoost("Meta Pass Emissive Boost", Float) = 1.0
        _TargetAspectRatio("Target Aspect Ratio", Float) = 1.7777777
        [Toggle(_)]_IsAVProInput("Is AV Pro Input", Int) = 0
    }

        CGINCLUDE
#define UNITY_SETUP_BRDF_INPUT MetallicSetup
            ENDCG

            SubShader
        {
            Tags { "RenderType" = "Opaque" "PerformanceChecks" = "False" }
            LOD 300


            // ------------------------------------------------------------------
            //  Base forward pass (directional light, emission, lightmaps, ...)
            Pass
            {
                Name "FORWARD"
                Tags { "LightMode" = "ForwardBase" }

                Blend[_SrcBlend][_DstBlend]
                ZWrite[_ZWrite]

                CGPROGRAM
                #pragma target 3.0

            // -------------------------------------

            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature _EMISSION
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature ___ _DETAIL_MULX2
            #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature _ _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature _ _GLOSSYREFLECTIONS_OFF
            #pragma shader_feature _PARALLAXMAP

            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            // Uncomment the following line to enable dithering LOD crossfade. Note: there are more in the file to uncomment for other passes.
            //#pragma multi_compile _ LOD_FADE_CROSSFADE

            #pragma vertex vertBase
            #pragma fragment fragBase
                #include "StandardVideoCore.cginc"

            ENDCG
        }
            // ------------------------------------------------------------------
            //  Additive forward pass (one light per pass)
            Pass
            {
                Name "FORWARD_DELTA"
                Tags { "LightMode" = "ForwardAdd" }
                Blend[_SrcBlend] One
                Fog { Color(0,0,0,0) } // in additive pass fog should be black
                ZWrite Off
                ZTest LEqual

                CGPROGRAM
                #pragma target 3.0

            // -------------------------------------


            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature _ _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature ___ _DETAIL_MULX2
            #pragma shader_feature _PARALLAXMAP

            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog
            // Uncomment the following line to enable dithering LOD crossfade. Note: there are more in the file to uncomment for other passes.
            //#pragma multi_compile _ LOD_FADE_CROSSFADE

            #pragma vertex vertAdd
            #pragma fragment fragAdd
            #include "StandardVideoCore.cginc"

            ENDCG
        }
            // ------------------------------------------------------------------
            //  Shadow rendering pass
            Pass {
                Name "ShadowCaster"
                Tags { "LightMode" = "ShadowCaster" }

                ZWrite On ZTest LEqual

                CGPROGRAM
                #pragma target 3.0

            // -------------------------------------


            #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature _PARALLAXMAP
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            // Uncomment the following line to enable dithering LOD crossfade. Note: there are more in the file to uncomment for other passes.
            //#pragma multi_compile _ LOD_FADE_CROSSFADE

            #pragma vertex vertShadowCaster
            #pragma fragment fragShadowCaster

            #include "UnityStandardShadow.cginc"

            ENDCG
        }
            // ------------------------------------------------------------------
            //  Deferred pass
            Pass
            {
                Name "DEFERRED"
                Tags { "LightMode" = "Deferred" }

                CGPROGRAM
                #pragma target 3.0
                #pragma exclude_renderers nomrt


            // -------------------------------------

            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature _EMISSION
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature _ _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature ___ _DETAIL_MULX2
            #pragma shader_feature _PARALLAXMAP

            #pragma multi_compile_prepassfinal
            #pragma multi_compile_instancing
            // Uncomment the following line to enable dithering LOD crossfade. Note: there are more in the file to uncomment for other passes.
            //#pragma multi_compile _ LOD_FADE_CROSSFADE

            #pragma vertex vertDeferred
            #pragma fragment fragDeferred

            #include "StandardVideoCore.cginc"

            ENDCG
        }

            // ------------------------------------------------------------------
            // Extracts information for lightmapping, GI (emission, albedo, ...)
            // This pass it not used during regular rendering.
            Pass
            {
                Name "META"
                Tags { "LightMode" = "Meta" }

                Cull Off

                CGPROGRAM
                #pragma vertex vert_meta
                #pragma fragment frag_meta

                #pragma shader_feature _EMISSION
                #pragma shader_feature _METALLICGLOSSMAP
                #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
                #pragma shader_feature ___ _DETAIL_MULX2
                #pragma shader_feature EDITOR_VISUALIZATION
                #include "UnityCG.cginc"
                #include "UnityStandardInput.cginc"
                #include "UnityMetaPass.cginc"
                #include "StandardVideoCore.cginc"

                struct v2f_meta
                {
                    float4 pos      : SV_POSITION;
                    float4 uv       : TEXCOORD0;
                #ifdef EDITOR_VISUALIZATION
                    float2 vizUV        : TEXCOORD1;
                    float4 lightCoord   : TEXCOORD2;
                #endif
                };

                float _MetaPassEmissiveBoost;

                v2f_meta vert_meta (VertexInput v)
                {
                    v2f_meta o;
                    o.pos = UnityMetaVertexPosition(v.vertex, v.uv1.xy, v.uv2.xy, unity_LightmapST, unity_DynamicLightmapST);
                    o.uv = TexCoords(v);
                #ifdef EDITOR_VISUALIZATION
                    o.vizUV = 0;
                    o.lightCoord = 0;
                    if (unity_VisualizationMode == EDITORVIZ_TEXTURE)
                        o.vizUV = UnityMetaVizUV(unity_EditorViz_UVIndex, v.uv0.xy, v.uv1.xy, v.uv2.xy, unity_EditorViz_Texture_ST);
                    else if (unity_VisualizationMode == EDITORVIZ_SHOWLIGHTMASK)
                    {
                        o.vizUV = v.uv1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
                        o.lightCoord = mul(unity_EditorViz_WorldToLight, mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)));
                    }
                #endif
                    return o;
                }

                // Albedo for lightmapping should basically be diffuse color.
                // But rough metals (black diffuse) still scatter quite a lot of light around, so
                // we want to take some of that into account too.
                half3 UnityLightmappingAlbedo (half3 diffuse, half3 specular, half smoothness)
                {
                    half roughness = SmoothnessToRoughness(smoothness);
                    half3 res = diffuse;
                    res += specular * roughness * 0.5;
                    return res;
                }

                float4 frag_meta (v2f_meta i) : SV_Target
                {
                    // we're interested in diffuse & specular colors,
                    // and surface roughness to produce final albedo.
                    FragmentCommonData data = UNITY_SETUP_BRDF_INPUT (i.uv);

                    UnityMetaInput o;
                    UNITY_INITIALIZE_OUTPUT(UnityMetaInput, o);

                #ifdef EDITOR_VISUALIZATION
                    o.Albedo = data.diffColor;
                    o.VizUV = i.vizUV;
                    o.LightCoord = i.lightCoord;
                #else
                    o.Albedo = UnityLightmappingAlbedo (data.diffColor, data.specColor, data.smoothness);
                #endif
                    o.SpecularColor = data.specColor;
                    o.Emission = VideoEmission(i.uv.xy) * _MetaPassEmissiveBoost;

                    return UnityMetaFragment(o);
                }

                ENDCG
            }
        }

            SubShader
        {
            Tags { "RenderType" = "Opaque" "PerformanceChecks" = "False" }
            LOD 150

            // ------------------------------------------------------------------
            //  Base forward pass (directional light, emission, lightmaps, ...)
            Pass
            {
                Name "FORWARD"
                Tags { "LightMode" = "ForwardBase" }

                Blend[_SrcBlend][_DstBlend]
                ZWrite[_ZWrite]

                CGPROGRAM
                #pragma target 2.0

                #pragma shader_feature _NORMALMAP
                #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
                #pragma shader_feature _EMISSION
                #pragma shader_feature _METALLICGLOSSMAP
                #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
                #pragma shader_feature _ _SPECULARHIGHLIGHTS_OFF
                #pragma shader_feature _ _GLOSSYREFLECTIONS_OFF
            // SM2.0: NOT SUPPORTED shader_feature ___ _DETAIL_MULX2
            // SM2.0: NOT SUPPORTED shader_feature _PARALLAXMAP

            #pragma skip_variants SHADOWS_SOFT DIRLIGHTMAP_COMBINED

            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog

            #pragma vertex vertBase
            #pragma fragment fragBase
            #include "StandardVideoCore.cginc"

            ENDCG
        }
            // ------------------------------------------------------------------
            //  Additive forward pass (one light per pass)
            Pass
            {
                Name "FORWARD_DELTA"
                Tags { "LightMode" = "ForwardAdd" }
                Blend[_SrcBlend] One
                Fog { Color(0,0,0,0) } // in additive pass fog should be black
                ZWrite Off
                ZTest LEqual

                CGPROGRAM
                #pragma target 2.0

                #pragma shader_feature _NORMALMAP
                #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
                #pragma shader_feature _METALLICGLOSSMAP
                #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
                #pragma shader_feature _ _SPECULARHIGHLIGHTS_OFF
                #pragma shader_feature ___ _DETAIL_MULX2
            // SM2.0: NOT SUPPORTED shader_feature _PARALLAXMAP
            #pragma skip_variants SHADOWS_SOFT

            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog

            #pragma vertex vertAdd
            #pragma fragment fragAdd
            #include "UnityStandardCoreForward.cginc"

            ENDCG
        }
            // ------------------------------------------------------------------
            //  Shadow rendering pass
            Pass {
                Name "ShadowCaster"
                Tags { "LightMode" = "ShadowCaster" }

                ZWrite On ZTest LEqual

                CGPROGRAM
                #pragma target 2.0

                #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
                #pragma shader_feature _METALLICGLOSSMAP
                #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
                #pragma skip_variants SHADOWS_SOFT
                #pragma multi_compile_shadowcaster

                #pragma vertex vertShadowCaster
                #pragma fragment fragShadowCaster

                #include "UnityStandardShadow.cginc"

                ENDCG
            }

            // ------------------------------------------------------------------
            // Extracts information for lightmapping, GI (emission, albedo, ...)
            // This pass it not used during regular rendering.
            Pass
            {
                Name "META"
                Tags { "LightMode" = "Meta" }

                Cull Off

                CGPROGRAM
                #pragma vertex vert_meta
                #pragma fragment frag_meta

                #pragma shader_feature _EMISSION
                #pragma shader_feature _METALLICGLOSSMAP
                #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
                #pragma shader_feature ___ _DETAIL_MULX2
                #pragma shader_feature EDITOR_VISUALIZATION

                #include "UnityStandardMeta.cginc"
                ENDCG
            }
        }


            FallBack "VertexLit"
            CustomEditor "StandardVideoEmissiveShaderGUI"
}
