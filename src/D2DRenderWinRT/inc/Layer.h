#pragma once
#include "D2DEngine.Layer.g.h"
#include <wrl/client.h>
using namespace Microsoft::WRL;

namespace winrt::D2DEngine::implementation
{
	struct Layer : LayerT<Layer>
	{
		~Layer() noexcept = default;

		Layer(hstring const& name, int32_t width, int32_t height, LayerType type);

		// ==== 属性接口 ====
		hstring Name();
		void Name(hstring const& value);

		int32_t Width();
		int32_t Height();

		LayerType Type();

		bool Visible();
		void Visible(bool value);

		double Opacity();
		void Opacity(double value);

		// ==== 尺寸与变换 ====
		void Resize(int32_t newWidth, int32_t newHeight);
		void Rotate(double angleDegrees);
		void FlipHorizontal();
		void FlipVertical();

		// ==== 渲染相关 ====
		ComPtr<ID2D1Bitmap1> GetBitmap();
		ComPtr<ID2D1DeviceContext5> GetContext();

		// ===== 绘图工具 =====
		void DrawStroke(std::vector<D2D1_POINT_2F> const& points, StrokeOptions const& options);
		void FillRect(D2D1_RECT_F const& rect, FillOptions const& options);
		void FillPath(std::vector<D2D1_POINT_2F> const& points, FillOptions const& options);
		void FloodFill(int x, int y, FillOptions const& options);

		// ===== 选择工具 =====
		void CaptureSelection(D2D1_RECT_F const& rect);                  // 捕获选区内容
		void MoveSelection(D2D1_POINT_2F delta);                         // 拖动选区
		void RenderWithSelection(ID2D1DeviceContext5* ctx);             // 渲染选区及控制点
		void PlaceSelection();                                           // 放置选区到 Layer
		void CancelSelection();                                          // 撤销选区
		SelectionHandle HitTestSelectionHandle(D2D1_POINT_2F pt);       // 点击控制点或内部
		void ResizeSelection(D2D1_POINT_2F newPt, SelectionHandle handle); // 根据控制点调整大小

		// ===== 裁剪工具 =====
		void StartCrop(D2D1_POINT_2F startPoint);                   // 鼠标按下，开始拖出裁剪框
		void UpdateCrop(D2D1_POINT_2F currentPoint);                // 鼠标拖动更新裁剪框
		void RenderCrop(ID2D1DeviceContext5* ctx);                  // 渲染裁剪框 + 灰色蒙层
		void ConfirmCrop();                                         // 确认裁剪，生成新 bitmap 或修改原 Layer
		void CancelCrop();                                          // 取消裁剪
		CropHandleType HitTestCropHandle(D2D1_POINT_2F pt);       // 鼠标检测是否点击控制点或内部
		void ResizeCrop(D2D1_POINT_2F newPt, CropHandleType handle); // 调整裁剪框大小

		// ===== 图层变换 =====
		void ResizeLayer(int32_t newWidth, int32_t newHeight); // 改变图层大小，保持内容缩放
		void RotateLayer(double angleDegrees);                // 顺时针旋转角度
		void FlipHorizontalLayer();                            // 水平翻转
		void FlipVerticalLayer();                              // 垂直翻转

	private:
		hstring m_name;
		int32_t m_width;
		int32_t m_height;
		LayerType m_type;
		bool m_visible{ true };
		double m_opacity{ 1.0 };

		// 每个图层自己的 D2D 目标
		ComPtr<ID2D1Bitmap1> m_bitmap;

		// ===== 选择工具成员 =====
		Selection m_selection;

		// 裁剪工具成员
		Crop m_crop;
		float m_cropHandleSize{ 8.0f };

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
