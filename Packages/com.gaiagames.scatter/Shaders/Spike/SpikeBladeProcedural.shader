// Phase 1 spike: blades built from SV_VertexID, no mesh (BRG BatchDrawCommandProceduralIndirect, RenderPrimitivesIndirect),
// one blade per instance (non-indexed) or K blades per instance (indexed, _Indexed = 1).
// Hand-written HLSL, no alpha clipping, the five passes of design 8.1. Code in SpikeBladeInput.hlsl and SpikePasses.hlsl.
Shader "Hidden/Scatter/Spike/Blade Procedural"
{
    Properties
    {
        _BaseColor("Root colour", Color) = (0.06, 0.2, 0.03, 1)
        _TipColor("Tip colour", Color) = (0.42, 0.62, 0.18, 1)
        _DebugColor("Debug colour", Color) = (1, 0, 0, 1)
        _BladeWidth("Width (m)", Float) = 0.04
        _BladeHeight("Height (m)", Float) = 0.45
        _BladeBend("Bend (fraction of height)", Float) = 0.25
        _SegmentCount("Segments", Float) = 3
        _BladesPerInstance("Blades per instance", Float) = 1
        _Indexed("Indexed", Float) = 0
        _RegionStart("Region start", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex SpikeVertex
            #pragma fragment SpikeFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #define SPIKE_PASS_FORWARD
            #define SPIKE_BLADE_PROCEDURAL
            #include "Packages/com.gaiagames.scatter/Shaders/Spike/SpikeBladeInput.hlsl"
            #include "Packages/com.gaiagames.scatter/Shaders/Spike/SpikePasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex SpikeVertex
            #pragma fragment SpikeFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #define SPIKE_PASS_SHADOW_CASTER
            #define SPIKE_BLADE_PROCEDURAL
            #include "Packages/com.gaiagames.scatter/Shaders/Spike/SpikeBladeInput.hlsl"
            #include "Packages/com.gaiagames.scatter/Shaders/Spike/SpikePasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex SpikeVertex
            #pragma fragment SpikeFragment
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #define SPIKE_PASS_DEPTH_ONLY
            #define SPIKE_BLADE_PROCEDURAL
            #include "Packages/com.gaiagames.scatter/Shaders/Spike/SpikeBladeInput.hlsl"
            #include "Packages/com.gaiagames.scatter/Shaders/Spike/SpikePasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex SpikeVertex
            #pragma fragment SpikeFragment
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #define SPIKE_PASS_DEPTH_NORMALS
            #define SPIKE_BLADE_PROCEDURAL
            #include "Packages/com.gaiagames.scatter/Shaders/Spike/SpikeBladeInput.hlsl"
            #include "Packages/com.gaiagames.scatter/Shaders/Spike/SpikePasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            ColorMask RG

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex SpikeVertex
            #pragma fragment SpikeFragment
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #define SPIKE_PASS_MOTION_VECTORS
            #define SPIKE_BLADE_PROCEDURAL
            #include "Packages/com.gaiagames.scatter/Shaders/Spike/SpikeBladeInput.hlsl"
            #include "Packages/com.gaiagames.scatter/Shaders/Spike/SpikePasses.hlsl"
            ENDHLSL
        }
    }
}
