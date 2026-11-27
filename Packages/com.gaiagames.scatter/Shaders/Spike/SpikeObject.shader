// Phase 1 spike: Object layer (small rocks), instances of an imported mesh (BRG BatchDrawCommandIndirect).
// Hand-written HLSL, no alpha clipping, the five passes of design 8.1. Code in SpikeObjectInput.hlsl and SpikePasses.hlsl.
Shader "Hidden/Scatter/Spike/Object"
{
    Properties
    {
        _BaseColor("Bottom colour", Color) = (0.22, 0.2, 0.18, 1)
        _TipColor("Top colour", Color) = (0.5, 0.48, 0.44, 1)
        _ObjectShape("Scale XYZ (m), random scale range", Vector) = (0.3, 0.18, 0.25, 0.4)
        _DebugColor("Debug colour", Color) = (1, 0, 0, 1)
        _BladesPerInstance("Blades per instance", Float) = 1
        _RegionStart("Region start", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull Back

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
            #include "Packages/com.gaiagames.scatter/Shaders/Spike/SpikeObjectInput.hlsl"
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
            #include "Packages/com.gaiagames.scatter/Shaders/Spike/SpikeObjectInput.hlsl"
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
            #include "Packages/com.gaiagames.scatter/Shaders/Spike/SpikeObjectInput.hlsl"
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
            #include "Packages/com.gaiagames.scatter/Shaders/Spike/SpikeObjectInput.hlsl"
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
            #include "Packages/com.gaiagames.scatter/Shaders/Spike/SpikeObjectInput.hlsl"
            #include "Packages/com.gaiagames.scatter/Shaders/Spike/SpikePasses.hlsl"
            ENDHLSL
        }
    }
}
