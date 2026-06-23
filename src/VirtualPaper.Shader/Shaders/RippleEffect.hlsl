// 涟漪效果 — 正弦波扭曲像素采样坐标
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).

#define D2D_INPUT_COUNT 1
#define D2D_INPUT0_COMPLEX
#define D2D_REQUIRES_SCENE_POSITION

#include "d2d1effecthelpers.hlsli"

cbuffer constants : register(b0)
{
    float frequency : packoffset(c0.x);
    float phase : packoffset(c0.y);
    float amplitude : packoffset(c0.z);
    float spread : packoffset(c0.w);
    float2 center   : packoffset(c1);
    float dpi : packoffset(c1.z);
};

D2D_PS_ENTRY(main)
{
    float2 pos = D2DGetScenePosition().xy;
    float2 toPixel = pos - center;
    
    // Distance from center, scaled by DPI
    float distance = length(toPixel) * (96.0f / dpi);
    
    // Direction from center (use default right direction if at center)
    float len = length(toPixel);
    float2 direction = len > 0.001f ? toPixel / len : float2(1.0f, 0.0f);

    // Wave calculation
    float2 wave;
    sincos(frequency * distance * 0.02f + phase, wave.x, wave.y);

    // Uniform amplitude - no distance-based falloff
    float2 inputOffset = wave.x * amplitude * 0.5f * direction;
    
    // Subtle lighting effect
    float lighting = lerp(1.0f, 1.0f + wave.x * 0.1f, saturate(amplitude / 40.0f));

    float4 color = D2DSampleInputAtOffset(0, inputOffset);
    color.rgb *= lighting;

    return color;
}