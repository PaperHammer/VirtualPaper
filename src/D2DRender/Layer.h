#pragma once
#include "D2DEngine.Layer.g.h"
#include <wrl.h>
#include <d2d1_3.h>
using namespace std;
using namespace Microsoft::WRL;

namespace winrt::D2DEngine::implementation
{
	struct Layer : LayerT<Layer>
	{
		~Layer() noexcept = default;

		Layer(hstring const& name, int32_t width, int32_t height, LayerType type);

		// 属性
		hstring Name();
		void Name(hstring const& value);

		int32_t Width() const;
		int32_t Height() const;

		LayerType Type() const;

		bool Visible() const;
		void Visible(bool value);

		double Opacity() const;
		void Opacity(double value);

		// 尺寸与变换
		static constexpr double DEFAULT_ROTATION_ANGLE = 90.0;
		void Resize(int32_t newWidth, int32_t newHeight, bool scaleContent);
		void Rotate(RotateDirection direction);
		void FlipHorizontal();
		void FlipVertical();

		// 渲染相关
		ComPtr<ID2D1Bitmap1> GetBitmap();
		ComPtr<ID2D1DeviceContext5> GetContext();

		// 绘图
		void DrawStroke(vector<D2D1_POINT_2F> const& points, StrokeOptions const& options);
		void FillRect(D2D1_RECT_F const& rect, FillOptions const& options);
		void FillPath(vector<D2D1_POINT_2F> const& points, FillOptions const& options);
		void FloodFill(int x, int y, FillOptions const& options);

		// 选择
		AreaHandle HitTestSelectionHandle(D2D1_POINT_2F pt) const;
		void ResizeSelection(D2D1_POINT_2F newPt, AreaHandle handle);

		// 裁剪	
		AreaHandle HitTestCropHandle(D2D1_POINT_2F pt) const;
		void ResizeCrop(D2D1_POINT_2F newPt, AreaHandle handle);

	private:
		hstring m_name;
		uint32_t m_width;
		uint32_t m_height;
		LayerType m_type;
		bool m_visible{ true };
		double m_opacity{ 1.0 };

		// 每个图层自己的 D2D 目标
		ComPtr<ID2D1Bitmap1> m_bitmap;

		Area m_selection;
		ComPtr<ID2D1Bitmap1> m_selectionBitmap;
		
		Area m_crop;		
		ComPtr<ID2D1Bitmap1> m_cropBitmap;

		// 当前图层变换状态
		double m_rotation{ 0.0 }; // 角度
		bool m_flipH{ false };
		bool m_flipV{ false };
	};
}

namespace winrt::D2DEngine::factory_implementation
{
	struct Layer : LayerT<Layer, implementation::Layer> {};
}

