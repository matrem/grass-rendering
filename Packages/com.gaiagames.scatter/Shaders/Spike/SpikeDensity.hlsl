#ifndef SCATTER_SPIKE_DENSITY_INCLUDED
#define SCATTER_SPIKE_DENSITY_INCLUDED

// Phase 1b density curve over distance (design 5.2, 5.3), shared by the cull compute (keep rank < density) and the
// blade shaders (widening). Mirrored in C# by SpikeDensityCurve.cs (Runtime/Rendering/Spike).
// Shaders receive the values as globals; the compute receives them as compute parameters.

float4 _SpikeDensityDistances;   // x = near, y = mid, z = far (meters)
float4 _SpikeDensityValues;      // x = mid density, y = far density, z = widen max factor (1 = no widening)
float4 _SpikeCullCameraPosition; // xyz: camera of the cull. Not _WorldSpaceCameraPos: in the shadow pass it is the light.

#define SPIKE_MIN_DENSITY 1e-4

// 1 up to near, linear to the mid density at mid and to the far density at far, constant beyond.
// Single return: early returns make FXC warn about a potentially uninitialized value when the function is inlined.
float SpikeDensityAtDistance(float distanceMeters)
{
    float nearMeters = _SpikeDensityDistances.x;
    float midMeters = _SpikeDensityDistances.y;
    float farMeters = _SpikeDensityDistances.z;
    float density = _SpikeDensityValues.y;
    if (distanceMeters <= nearMeters)
    {
        density = 1.0;
    }
    else if (distanceMeters <= midMeters)
    {
        density = lerp(1.0, _SpikeDensityValues.x, (distanceMeters - nearMeters) / (midMeters - nearMeters));
    }
    else if (distanceMeters <= farMeters)
    {
        density = lerp(_SpikeDensityValues.x, _SpikeDensityValues.y, (distanceMeters - midMeters) / (farMeters - midMeters));
    }

    return density;
}

// Fewer, wider far blades (design 10.1): 1/sqrt(density), at most the widen max factor.
float SpikeWidenFactor(float density)
{
    return min(1.0 / sqrt(max(density, SPIKE_MIN_DENSITY)), _SpikeDensityValues.z);
}

#endif
