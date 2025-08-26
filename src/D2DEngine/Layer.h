#pragma once
#include "Layer.g.h"
#include <d2d1_3.h>
using std::vector;

namespace winrt::D2DEngine::implementation
{
	struct Layer : LayerT<Layer>
	{
		~Layer() noexcept = default;
		Layer(hstring const& name, int32_t width, int32_t height, winrt::D2DEngine::LayerType type);

		// 属性
		hstring Name();
		void Name(hstring const& value);

		int32_t Width() const;
		int32_t Height() const;

		winrt::D2DEngine::LayerType Type() const;

		bool Visible() const;
		void Visible(bool value);

		double Opacity() const;
		void Opacity(double value);

		// 尺寸与变换
		static constexpr double DEFAULT_ROTATION_ANGLE = 90.0;
		void Resize(int32_t newWidth, int32_t newHeight, bool scaleContent);
		void Rotate(winrt::D2DEngine::RotateDirection direction);
		void FlipHorizontal();
		void FlipVertical();

		// 渲染
		winrt::Windows::Foundation::IAsyncOperation<winrt::Windows::Storage::Streams::IBuffer> RenderToBufferAsync();
		winrt::com_ptr<ID2D1Bitmap1> GetBitmap();
		winrt::com_ptr<ID2D1DeviceContext5> GetContext();

		// 绘图
		void DrawStroke(winrt::Windows::Foundation::Collections::IVector<winrt::Windows::Foundation::Point> const& points, winrt::D2DEngine::StrokeOptions const& options);
		void FillRect(winrt::Windows::Foundation::Rect const& rect, winrt::D2DEngine::FillOptions const& options);
		void FillPath(winrt::Windows::Foundation::Collections::IVector<winrt::Windows::Foundation::Point> const& points, winrt::D2DEngine::FillOptions const& options);
		void FloodFill(int x, int y, winrt::D2DEngine::FillOptions const& options);

		// 选择
		winrt::D2DEngine::AreaHandle HitTestSelectionHandle(winrt::Windows::Foundation::Point pt) const;
		void ResizeSelection(winrt::Windows::Foundation::Point newPt, winrt::D2DEngine::AreaHandle handle);

		// 裁剪	
		winrt::D2DEngine::AreaHandle HitTestCropHandle(winrt::Windows::Foundation::Point pt) const;
		void ResizeCrop(winrt::Windows::Foundation::Point newPt, winrt::D2DEngine::AreaHandle handle);

	private:
		hstring m_name;
		uint32_t m_width;
		uint32_t m_height;
		winrt::D2DEngine::LayerType m_type;
		bool m_visible{ true };
		double m_opacity{ 1.0 };

		// 每个图层自己的 D2D 目标
		winrt::com_ptr<ID2D1Bitmap1> m_bitmap;
		// 每个图层自己的上下文
		winrt::com_ptr<ID2D1DeviceContext5> m_ctx;

		winrt::D2DEngine::Area m_selection;
		winrt::com_ptr<ID2D1Bitmap1> m_selectionBitmap;

		winrt::D2DEngine::Area m_crop;
		winrt::com_ptr<ID2D1Bitmap1> m_cropBitmap;

		// 图层变换状态
		double m_rotation{ 0.0 }; // 角度
		bool m_flipH{ false };
		bool m_flipV{ false };		

		// 绘制
		vector<D2D1_POINT_2F> GenerateSmoothCurve(const vector<D2D1_POINT_2F>& points);
		void CreatePathGeometry(const vector<D2D1_POINT_2F>& points, ID2D1PathGeometry** geometry);
		void CreateEraserStrokeStyle(ID2D1StrokeStyle** style);
	};
}

namespace winrt::D2DEngine::factory_implementation
{
	struct Layer : LayerT<Layer, implementation::Layer> {};
}
