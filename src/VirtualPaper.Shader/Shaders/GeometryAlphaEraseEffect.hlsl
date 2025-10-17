#define D2D_INPUT_COUNT 2
#define D2D_INPUT0_SIMPLE // 原图 snapshot
#define D2D_INPUT1_SIMPLE // 几何 mask
#include "d2d1effecthelpers.hlsli"

float eraseAmount; // [0,1]

D2D_PS_ENTRY(main)
{
    float4 original = D2DGetInput(0);
    float4 mask = D2DGetInput(1);

    // mask灰度：白色表示geometry内部
    float maskValue = max(mask.a, dot(mask.rgb, float3(0.299, 0.587, 0.114)));

    if (maskValue <= 0.001)
        return original;

    // 应用擦除效果
    float eraseFactor = eraseAmount * maskValue;
    float finalAlpha = original.a * (1.0 - eraseFactor);
    
    // 预乘alpha计算
    float alphaRatio = (original.a > 0.0) ? (finalAlpha / original.a) : 0.0;
    float3 finalColor = original.rgb * alphaRatio;
    
    return float4(finalColor, finalAlpha);
}
