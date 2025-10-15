#define D2D_INPUT_COUNT 1
#define D2D_INPUT0_SIMPLE
#define D2D_REQUIRES_SCENE_POSITION
#include "d2d1effecthelpers.hlsli"

int soft; // 边缘渐变类型 (0-4)
float eraseAmount; // 擦除强度 [0,1]
float radius; // 擦除半径
float2 targetPosition; // 擦除中心位置

float Cosine(float value)
{
    return cos(value * 3.14159274) / 2 + 0.5;
}

float GetRadiusAlpha(float x)
{
    // None - 完全擦除（无渐变）
    if (soft == 0)
        return 1;
    
    // Cosine
    if (soft == 1)
        return Cosine(x);
    
    // Quadratic
    if (soft == 2)
        return Cosine(x * x);
    
    // Cube
    if (soft == 3)
        return Cosine(x * x * x);
    
    // Quartic
    if (soft == 4)
        return Cosine(x * x * x * x);

    return 1;
}

D2D_PS_ENTRY(main)
{
    float2 p = D2DGetScenePosition().xy;
    float4 originalColor = D2DGetInput(0);
    
    float dist = distance(targetPosition, p);
    float distRatio = dist / radius;
    
    // In-Radius
    if (distRatio > 1.0) return originalColor;

    // None
    if(soft == 0) 
    {
        // Alpha : 0~1
        float alpha1 = smoothstep(0, 1, originalColor.a * eraseAmount);
        return float4(originalColor.rgb, originalColor.a * (1 - eraseAmount));
    }
    else
    {
        // Alpha : 0~1
        float alpha2 = smoothstep(0, 1, originalColor.a * (1 - eraseAmount * GetRadiusAlpha(distRatio)));
        return float4(originalColor.rgb, alpha2);
    }
}