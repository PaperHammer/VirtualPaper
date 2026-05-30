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
    float2 toPixel = D2DGetScenePosition().xy - center;
    float distance = length(toPixel * (96.0f / dpi / 500.0f));
    float2 direction = normalize(toPixel);

    float2 wave;
    sincos(frequency * distance + phase, wave.x, wave.y);

    float falloff = saturate(1.0f - distance);
    falloff = pow(falloff, 1.0f / spread);

    float2 inputOffset = (wave.x * falloff * amplitude) * direction;
    float lighting = lerp(1.0f, 1.0f + wave.x * falloff * 0.2f, saturate(amplitude / 20.0f));

    float4 color = D2DSampleInputAtOffset(0, inputOffset);
    color.rgb *= lighting;

    return color;
}