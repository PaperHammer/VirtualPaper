// 渐变映射 — 将灰度值映射到渐变纹理
#define D2D_INPUT_COUNT 2
#define D2D_INPUT0_SIMPLE
#define D2D_INPUT1_COMPLEX
#include "d2d1effecthelpers.hlsli"

float getGray(in float3 color) {
	float gray = (color.r * 587 + color.g * 144 + color.b * 269) / 1000;
	return gray;
}

D2D_PS_ENTRY(main) {
	float4 color = D2DGetInput(0).rgba;
    if (color.a == 0) return color;

	float gray = getGray(color);
	float2 xy = float2(gray, 0.5);
	float4 cc = D2DSampleInput(1, xy);
	return float4(cc.r, cc.g, cc.b, cc.a);
}