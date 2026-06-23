// 阈值效果 — 按灰度值将像素二分为两种颜色
#define D2D_INPUT_COUNT 1
#define D2D_INPUT0_SIMPLE

#include "d2d1effecthelpers.hlsli"

float threshold; // 0~3
float4 color0; 
float4 color1;

D2D_PS_ENTRY(main) 
{
	float4 color = D2DGetInput(0);
	float a = color.a;
    
    if(a == 0)
        return float4(0, 0, 0, 0);

	float gray= color.r + color.g + color.b;
    
    if(a == 1)
    {
    	if(gray <= threshold)
	    	return color1;
	    else
	    	return color0;
    }
    else
    {
    	if(gray <= threshold)
            return float4(color1.r, color1.g, color1.b, a);
	    else
            return float4(color0.r, color0.g, color0.b, a);
    }
}