// 擦除工具像素着色器 — 对 Mask 覆盖的区域透明度衰减

Texture2D sourceTex : register(t0); // 原始画布
Texture2D maskTex : register(t1); // 当前擦除几何生成的 Mask
SamplerState samLinear : register(s0);

cbuffer BrushParams : register(b0)
{
    float EraseAmount; // [0,1] 每次擦除衰减比例
}

float4 main(float2 uv : TEXCOORD) : SV_TARGET
{
    float4 src = sourceTex.Sample(samLinear, uv);
    float4 mask = maskTex.Sample(samLinear, uv);

    // 白色 mask 表示擦除区域，黑色表示保留
    float eraseFactor = saturate(mask.r) * EraseAmount;

    // 衰减 alpha（非线性柔和）
    src.a = lerp(src.a, 0.0f, eraseFactor);

    return src;
}
