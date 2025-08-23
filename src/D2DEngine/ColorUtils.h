#pragma once
#include <d2d1_3.h>
#include <winrt/Windows.Foundation.Numerics.h>

class ColorConverter
{
public:
	// Vector4 转 D2D1_COLOR_F
	static inline  D2D1_COLOR_F WinRTToD2DColor(const winrt::Windows::Foundation::Numerics::float4& vector)
	{
		return D2D1::ColorF(vector.x, vector.y, vector.z, vector.w);
	}

	// D2D1_COLOR_F 转 Vector4
	static inline winrt::Windows::Foundation::Numerics::float4 D2DColorToWinRT(const D2D1_COLOR_F& color)
	{
		return { color.r, color.g, color.b, color.a };
	}

	// 带不透明度的转换
	static inline D2D1_COLOR_F WinRTToD2DColorWithOpacity(
		const winrt::Windows::Foundation::Numerics::float4& vector,
		float opacity)
	{
		return D2D1::ColorF(vector.x, vector.y, vector.z, vector.w * opacity);
	}

	static inline winrt::Windows::Foundation::Numerics::float4 D2DColorToWinRTWithOpacity(
		const D2D1_COLOR_F& color,
		float opacity)
	{
		return { color.r, color.g, color.b, color.a * opacity };
	}
};

class ColorUtils {
public:
	static inline bool ColorWithinTolerance(D2D1_COLOR_F a, D2D1_COLOR_F b, float tolerance)
	{
		float dr = a.r - b.r;
		float dg = a.g - b.g;
		float db = a.b - b.b;
		float da = a.a - b.a;
		float dist = std::sqrt(dr * dr + dg * dg + db * db + da * da);
		return dist <= tolerance;
	}
};
