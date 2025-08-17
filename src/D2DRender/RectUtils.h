#pragma once
#include <d2d1_3.h>
#include <winrt/Windows.Foundation.h>

class RectConverter
{
public:
	// Windows.Foundation.Rect ת D2D1_RECT_F
	static inline D2D1_RECT_F WinRTToD2D(const winrt::Windows::Foundation::Rect& rect)
	{
		return D2D1::RectF(
			static_cast<float>(rect.X),
			static_cast<float>(rect.Y),
			static_cast<float>(rect.X + rect.Width),
			static_cast<float>(rect.Y + rect.Height)
		);
	}

	// D2D1_RECT_F ת Windows.Foundation.Rect
	static inline winrt::Windows::Foundation::Rect D2DToWinRT(const D2D1_RECT_F& rect)
	{
		return winrt::Windows::Foundation::Rect(
			rect.left,
			rect.top,
			rect.right - rect.left,  // Width
			rect.bottom - rect.top   // Height
		);
	}
};

class RectUtils {
public:
	static std::vector<D2D1_POINT_2F> GetControlPoints(const D2D1_RECT_F& rect, float handleSize)
	{
		float centerX = rect.left + (rect.right - rect.left) / 2.0f;
		float centerY = rect.top + (rect.bottom - rect.top) / 2.0f;

		return {
			{rect.left, rect.top}, // 左上
			{centerX, rect.top}, // 中上
			{rect.right, rect.top}, // 右上
			{rect.left, centerY}, // 左中
			{rect.right, centerY}, // 右中
			{rect.left, rect.bottom}, // 左下
			{centerX, rect.bottom}, // 中下
			{rect.right, rect.bottom} // 右下
		};
	}
};
