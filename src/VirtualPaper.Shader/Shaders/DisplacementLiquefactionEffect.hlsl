// 液化效果 — 推/挤压/旋转像素位移（DisplacementMap）
#define D2D_INPUT_COUNT 1
#define D2D_INPUT0_COMPLEX
#define D2D_REQUIRES_SCENE_POSITION
#include "d2d1effecthelpers.hlsli"

int mode;

float amount;
float pressure;

float radius;
float2 targetPosition;
float2 position;

float4 GetVectorColor(float4 color, float opacity, float2 p0, float2 p1)
{
    float2 vect = p0 - p1;
    float lenght = distance(p0, p1);
    float2 unit = mul(vect, 1 / lenght);

    color.r -= 12 * opacity * unit.x / amount;
    color.g -= 12 * opacity * unit.y / amount;
    
    return color;
}

float4 GetAngleColor(float4 color, float angle, float2 vect)
{
    float sin2 = sin(angle);
    float cos2 = cos(angle);

    float x = vect.x * cos2 - vect.y * sin2;
    float y = vect.x * sin2 + vect.y * cos2;

    float unitX = x - vect.x;
    float unitY = y - vect.y;

    color.r -= unitX / amount;
    color.g -= unitY / amount;

    return color;
}

float4 GetBlendColor(float4 color, float opacity)
{
    float opacityHalf = opacity * 0.5f;
    float opacityR = 1 - opacity;

    color.r = opacityHalf + opacityR * color.r;
    color.g = opacityHalf + opacityR * color.g;

    return color;
}

D2D_PS_ENTRY(main)
{
    float2 p = D2DGetScenePosition().xy;
    float4 color = D2DGetInput(0);

    float dist = distance(targetPosition, p);
    if (dist > radius) return color;

    float x = dist / radius;
    float opacity = pressure * (1 - x * x);

    // TwirlRight
    if(mode == 5) return GetAngleColor(color, 3.141592654f / 12 * opacity, p - targetPosition);

    // TwirlLeft
    if(mode == 4) return GetAngleColor(color, -3.141592654f / 12 * opacity, p - targetPosition);

    // Expand
    if(mode == 3) return GetVectorColor(color, opacity, p, targetPosition);

    // Pinch
    if(mode == 2) return GetVectorColor(color, opacity, targetPosition, p);

    // Push
    if(mode == 1) return GetVectorColor(color, opacity, targetPosition, position);

    // Reset
    return GetBlendColor(color, opacity / 12);
}