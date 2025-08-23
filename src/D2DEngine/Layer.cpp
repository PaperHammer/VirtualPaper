#include "pch.h"
#include "Layer.h"
#include "Layer.g.cpp"
#include "ColorUtils.h"
#include "D2DDeviceManager.h"
#include "RectUtils.h"
#include <stack>
using Microsoft::WRL::ComPtr;

namespace winrt::D2DEngine::implementation
{
	Layer::Layer(hstring const& name, int32_t width, int32_t height, winrt::D2DEngine::LayerType type)
		: m_name(name), m_width(width), m_height(height), m_type(type)
	{
		m_bitmap = D2DDeviceManager::Instance().CreateRenderTargetForLayer(width, height);
	}

#pragma region 属性
	hstring Layer::Name() { return m_name; }
	void Layer::Name(hstring const& value) { m_name = value; }

	int32_t Layer::Width() const { return m_width; }
	int32_t Layer::Height() const { return m_height; }

	winrt::D2DEngine::LayerType Layer::Type() const { return m_type; }

	bool Layer::Visible() const { return m_visible; }
	void Layer::Visible(bool value) { m_visible = value; }

	double Layer::Opacity() const { return m_opacity; }
	void Layer::Opacity(double value) { m_opacity = std::clamp(value, 0.0, 1.0); }
#pragma endregion

#pragma region 尺寸与变换
	void Layer::Resize(int32_t newWidth, int32_t newHeight, bool scaleContent)
	{
		if (newWidth <= 0 || newHeight <= 0) {
			return;
		}

		if (newWidth == m_width && newHeight == m_height) {
			return;
		}

		ComPtr<ID2D1Bitmap1> newBitmap = D2DDeviceManager::Instance().CreateRenderTargetForLayer(newWidth, newHeight);
		if (!newBitmap) {
			// 创建失败，保持原状态
			return;
		}

		// 获取设备上下文
		auto ctx = D2DDeviceManager::Instance().GetD2DContext();
		ctx->SetTarget(newBitmap.Get());
		ctx->BeginDraw();
		ctx->Clear(D2D1::ColorF(0, 0, 0, 0)); // 透明背景

		if (scaleContent && m_bitmap) {
			// 需要缩放内容
			D2D1_RECT_F destRect = D2D1::RectF(0, 0, static_cast<float>(newWidth), static_cast<float>(newHeight));
			D2D1_RECT_F srcRect = D2D1::RectF(0, 0, static_cast<float>(m_width), static_cast<float>(m_height));

			// 使用高质量插值缩放
			ctx->DrawBitmap(
				m_bitmap.Get(),
				destRect,
				1.0f,
				D2D1_BITMAP_INTERPOLATION_MODE_LINEAR,
				srcRect
			);
		}
		else {
			// 不需要缩放内容，仅调整画布大小
			// 保持左上角内容不变，超出部分透明
			if (m_bitmap) {
				D2D1_RECT_F destRect = D2D1::RectF(0, 0,
					min(static_cast<float>(newWidth), static_cast<float>(m_width)),
					min(static_cast<float>(newHeight), static_cast<float>(m_height))
				);

				ctx->DrawBitmap(
					m_bitmap.Get(),
					destRect,
					1.0f,
					D2D1_BITMAP_INTERPOLATION_MODE_LINEAR,
					destRect
				);
			}
		}

		ctx->EndDraw();

		m_bitmap = newBitmap;
		m_width = newWidth;
		m_height = newHeight;
	}

	void Layer::Rotate(winrt::D2DEngine::RotateDirection direction)
	{
		// 确定旋转角度（顺时针为正，逆时针为负）
		double angle = (direction == winrt::D2DEngine::RotateDirection::Right) ? DEFAULT_ROTATION_ANGLE : -DEFAULT_ROTATION_ANGLE;

		// 更新累计旋转角度（标准化到0-360度范围）
		m_rotation = fmod(m_rotation + angle, 360.0);
		if (m_rotation < 0) m_rotation += 360.0;

		// 对于位图图层，需要实际旋转像素数据
		if (m_type == winrt::D2DEngine::LayerType::Bitmap && m_bitmap)
		{
			auto ctx = D2DDeviceManager::Instance().GetD2DContext();
			ctx->SetTarget(m_bitmap.Get());
			ctx->BeginDraw();
			ctx->Clear(D2D1::ColorF(0, 0, 0, 0));

			// 计算旋转后的尺寸（90度旋转时宽高交换）
			int newWidth = m_height;
			int newHeight = m_width;

			// 创建临时位图保存旋转结果
			auto rotatedBitmap = D2DDeviceManager::Instance().CreateRenderTargetForLayer(newWidth, newHeight);
			if (rotatedBitmap)
			{
				auto rotCtx = D2DDeviceManager::Instance().GetD2DContext();
				rotCtx->SetTarget(rotatedBitmap.Get());
				rotCtx->BeginDraw();
				rotCtx->Clear(D2D1::ColorF(0, 0, 0, 0));

				// 应用旋转
				rotCtx->SetTransform(D2D1::Matrix3x2F::Rotation(
					static_cast<float>(angle),
					D2D1::Point2F(newWidth / 2.0f, newHeight / 2.0f)
				));

				// 绘制原内容
				rotCtx->DrawBitmap(
					m_bitmap.Get(),
					D2D1::RectF(
						(newWidth - m_width) / 2.0f,
						(newHeight - m_height) / 2.0f,
						(newWidth + m_width) / 2.0f,
						(newHeight + m_height) / 2.0f
					)
				);

				rotCtx->EndDraw();

				// 更新图层位图和尺寸
				m_bitmap = rotatedBitmap;
				m_width = newWidth;
				m_height = newHeight;
			}

			ctx->EndDraw();
		}
	}

	void Layer::FlipHorizontal()
	{
		m_flipH = !m_flipH;

		// 对于位图图层，需要实际翻转像素数据
		if (m_type == winrt::D2DEngine::LayerType::Bitmap && m_bitmap)
		{
			auto ctx = D2DDeviceManager::Instance().GetD2DContext();
			ctx->SetTarget(m_bitmap.Get());
			ctx->BeginDraw();
			ctx->Clear(D2D1::ColorF(0, 0, 0, 0));

			// 创建临时位图保存翻转结果
			auto flippedBitmap = D2DDeviceManager::Instance().CreateRenderTargetForLayer(m_width, m_height);
			if (flippedBitmap)
			{
				auto flipCtx = D2DDeviceManager::Instance().GetD2DContext();
				flipCtx->SetTarget(flippedBitmap.Get());
				flipCtx->BeginDraw();
				flipCtx->Clear(D2D1::ColorF(0, 0, 0, 0));

				// 应用水平翻转
				flipCtx->SetTransform(D2D1::Matrix3x2F::Scale(
					-1.0f, 1.0f,
					D2D1::Point2F(m_width / 2.0f, m_height / 2.0f)
				));

				// 绘制原内容
				flipCtx->DrawBitmap(m_bitmap.Get());

				flipCtx->EndDraw();

				// 更新图层位图
				m_bitmap = flippedBitmap;
			}

			ctx->EndDraw();
		}
	}

	void Layer::FlipVertical()
	{
		m_flipV = !m_flipV;

		// 对于位图图层，需要实际翻转像素数据
		if (m_type == winrt::D2DEngine::LayerType::Bitmap && m_bitmap)
		{
			auto ctx = D2DDeviceManager::Instance().GetD2DContext();
			ctx->SetTarget(m_bitmap.Get());
			ctx->BeginDraw();
			ctx->Clear(D2D1::ColorF(0, 0, 0, 0));

			// 创建临时位图保存翻转结果
			auto flippedBitmap = D2DDeviceManager::Instance().CreateRenderTargetForLayer(m_width, m_height);
			if (flippedBitmap)
			{
				auto flipCtx = D2DDeviceManager::Instance().GetD2DContext();
				flipCtx->SetTarget(flippedBitmap.Get());
				flipCtx->BeginDraw();
				flipCtx->Clear(D2D1::ColorF(0, 0, 0, 0));

				// 应用垂直翻转
				flipCtx->SetTransform(D2D1::Matrix3x2F::Scale(
					1.0f, -1.0f,
					D2D1::Point2F(m_width / 2.0f, m_height / 2.0f)
				));

				// 绘制原内容
				flipCtx->DrawBitmap(m_bitmap.Get());

				flipCtx->EndDraw();

				// 更新图层位图
				m_bitmap = flippedBitmap;
			}

			ctx->EndDraw();
		}
	}
#pragma endregion

#pragma region 渲染
	ComPtr<ID2D1Bitmap1> Layer::GetBitmap()
	{
		return m_bitmap;
	}

	ComPtr<ID2D1DeviceContext5> Layer::GetContext()
	{
		auto ctx = D2DDeviceManager::Instance().GetD2DContext();
		ctx->SetTarget(m_bitmap.Get());
		return ctx;
	}
#pragma endregion

#pragma region 绘图
	void Layer::DrawStroke(vector<D2D1_POINT_2F> const& points, winrt::D2DEngine::StrokeOptions const& options)
	{
		if (!m_bitmap || points.size() < 2)
			return;

		auto ctx = D2DDeviceManager::Instance().GetD2DContext();
		ctx->SetTarget(m_bitmap.Get());
		ctx->BeginDraw();

		// 创建画笔
		ComPtr<ID2D1SolidColorBrush> brush;
		D2D1_COLOR_F color = ColorConverter::WinRTToD2DColor(options.Color);
		if (options.Tool == winrt::D2DEngine::StrokeTool::Eraser)
		{
			// 橡皮擦直接清空像素，使用透明
			color = D2D1::ColorF(0, 0); // alpha = 0
		}

		ctx->CreateSolidColorBrush(color, &brush);

		// 平滑曲线：使用 Catmull-Rom 样条插值
		vector<D2D1_POINT_2F> smoothPoints;
		smoothPoints.push_back(points[0]);
		for (size_t i = 1; i + 2 < points.size(); ++i)
		{
			D2D1_POINT_2F p0 = points[i - 1];
			D2D1_POINT_2F p1 = points[i];
			D2D1_POINT_2F p2 = points[i + 1];
			D2D1_POINT_2F p3 = points[i + 2];

			for (float t = 0; t <= 1.0f; t += 0.2f)
			{
				float t2 = t * t;
				float t3 = t2 * t;
				float x = 0.5f * ((2 * p1.x) + (-p0.x + p2.x) * t +
					(2 * p0.x - 5 * p1.x + 4 * p2.x - p3.x) * t2 +
					(-p0.x + 3 * p1.x - 3 * p2.x + p3.x) * t3);
				float y = 0.5f * ((2 * p1.y) + (-p0.y + p2.y) * t +
					(2 * p0.y - 5 * p1.y + 4 * p2.y - p3.y) * t2 +
					(-p0.y + 3 * p1.y - 3 * p2.y + p3.y) * t3);
				smoothPoints.push_back(D2D1::Point2F(x, y));
			}
		}
		smoothPoints.push_back(points.back());

		// 获取工厂对象（使用 D2DDeviceManager 提供的方法）
		ComPtr<ID2D1Factory7> factory = D2DDeviceManager::Instance().GetD2DFactory();

		// 创建路径几何
		ComPtr<ID2D1PathGeometry> geometry;
		factory->CreatePathGeometry(&geometry);

		ComPtr<ID2D1GeometrySink> sink;
		geometry->Open(&sink);
		sink->BeginFigure(smoothPoints[0], D2D1_FIGURE_BEGIN_HOLLOW);
		for (size_t i = 1; i < smoothPoints.size(); ++i)
		{
			sink->AddLine(smoothPoints[i]);
		}
		sink->EndFigure(D2D1_FIGURE_END_OPEN);
		sink->Close();

		// 创建笔触样式（可选）
		ComPtr<ID2D1StrokeStyle> strokeStyle;
		if (options.Tool == winrt::D2DEngine::StrokeTool::Eraser)
		{
			D2D1_STROKE_STYLE_PROPERTIES strokeProps = {};
			strokeProps.dashStyle = D2D1_DASH_STYLE_DASH;
			factory->CreateStrokeStyle(&strokeProps, nullptr, 0, &strokeStyle);
		}

		// 绘制几何图形
		ctx->DrawGeometry(
			geometry.Get(),        // 几何对象
			brush.Get(),           // 画笔
			options.Thickness,     // 线宽
			strokeStyle.Get()      // 笔触样式
		);

		ctx->EndDraw();
	}

	void Layer::FillRect(D2D1_RECT_F const& rect, winrt::D2DEngine::FillOptions const& options)
	{
		if (!m_bitmap) return;

		auto ctx = D2DDeviceManager::Instance().GetD2DContext();
		ctx->SetTarget(m_bitmap.Get());
		ctx->BeginDraw();

		// 创建填充画刷
		ComPtr<ID2D1SolidColorBrush> brush;
		D2D1_COLOR_F color = {
			options.Color.x,
			options.Color.y,
			options.Color.z,
			options.Color.w * options.Opacity
		};

		ctx->CreateSolidColorBrush(color, &brush);

		// 填充矩形
		ctx->FillRectangle(rect, brush.Get());

		ctx->EndDraw();
	}

	void Layer::FillPath(std::vector<D2D1_POINT_2F> const& points, winrt::D2DEngine::FillOptions const& options)
	{
		if (!m_bitmap || points.size() < 3) return;

		auto ctx = D2DDeviceManager::Instance().GetD2DContext();
		ctx->SetTarget(m_bitmap.Get());
		ctx->BeginDraw();

		// 创建路径几何
		ComPtr<ID2D1PathGeometry> path;
		D2DDeviceManager::Instance().GetD2DFactory()->CreatePathGeometry(&path);

		ComPtr<ID2D1GeometrySink> sink;
		path->Open(&sink);

		// 绘制路径
		sink->BeginFigure(points[0], D2D1_FIGURE_BEGIN_FILLED);
		for (size_t i = 1; i < points.size(); ++i) {
			sink->AddLine(points[i]);
		}
		sink->EndFigure(D2D1_FIGURE_END_CLOSED);
		sink->Close();

		// 创建填充画刷
		ComPtr<ID2D1SolidColorBrush> brush;
		D2D1_COLOR_F color = {
			options.Color.x,
			options.Color.y,
			options.Color.z,
			options.Color.w * options.Opacity
		};
		ctx->CreateSolidColorBrush(color, &brush);

		// 填充路径
		ctx->FillGeometry(path.Get(), brush.Get());

		ctx->EndDraw();
	}

	void Layer::FloodFill(int x, int y, winrt::D2DEngine::FillOptions const& options)
	{
		if (!m_bitmap) return;

		// 1. Map bitmap
		D2D1_MAPPED_RECT mapped;
		HRESULT hr = m_bitmap->Map(D2D1_MAP_OPTIONS_READ | D2D1_MAP_OPTIONS_WRITE, &mapped);
		if (FAILED(hr)) return;

		auto width = m_width;
		auto height = m_height;
		auto pixels = (uint32_t*)mapped.bits;

		// 2. 获取起点颜色
		auto index = y * mapped.pitch / 4 + x;
		if (index < 0 || index >= width * height)
		{
			m_bitmap->Unmap();
			return;
		}

		uint32_t targetPixel = pixels[index];

		// 转 D2D1_COLOR_F
		auto PixelToColor = [](uint32_t p) -> D2D1_COLOR_F
			{
				float a = ((p >> 24) & 0xFF) / 255.f;
				float r = ((p >> 16) & 0xFF) / 255.f;
				float g = ((p >> 8) & 0xFF) / 255.f;
				float b = (p & 0xFF) / 255.f;
				return D2D1::ColorF(r, g, b, a);
			};

		auto FillColorToPixel = [](D2D1_COLOR_F c) -> uint32_t
			{
				uint8_t a = (uint8_t)(c.a * 255);
				uint8_t r = (uint8_t)(c.r * 255);
				uint8_t g = (uint8_t)(c.g * 255);
				uint8_t b = (uint8_t)(c.b * 255);
				return (a << 24) | (r << 16) | (g << 8) | b;
			};

		D2D1_COLOR_F targetColor = PixelToColor(targetPixel);
		uint32_t fillPixel = FillColorToPixel(D2D1::ColorF(options.Color.x, options.Color.y, options.Color.z, options.Opacity));

		if (ColorUtils::ColorWithinTolerance(targetColor, ColorConverter::WinRTToD2DColor(options.Color), options.Tolerance))
		{
			m_bitmap->Unmap();
			return; // 相同颜色无需填充
		}

		// 3. 扫描线算法
		struct Span { int x1, x2, y; };
		std::stack<Span> stack;
		stack.push({ x, x, y });

		while (!stack.empty())
		{
			Span s = stack.top(); stack.pop();
			int left = s.x1;
			int right = s.x2;
			int row = s.y;

			// 向左扩展
			while (left >= 0 && ColorUtils::ColorWithinTolerance(PixelToColor(pixels[row * width + left]), targetColor, options.Tolerance))
				--left;
			++left;

			// 向右扩展
			while (right < width && ColorUtils::ColorWithinTolerance(PixelToColor(pixels[row * width + right]), targetColor, options.Tolerance))
				++right;
			--right;

			// 填充当前扫描线
			for (int i = left; i <= right; ++i)
				pixels[row * width + i] = fillPixel;

			// 上下行入栈
			for (int ny = row - 1; ny <= row + 1; ny += 2)
			{
				if (ny < 0 || ny >= height) continue;
				int nx = left;
				while (nx <= right)
				{
					bool spanFound = false;
					while (nx <= right && ColorUtils::ColorWithinTolerance(PixelToColor(pixels[ny * width + nx]), targetColor, options.Tolerance))
					{
						spanFound = true;
						++nx;
					}
					if (spanFound)
					{
						stack.push({ nx - 1, nx - 1, ny });
					}
					++nx;
				}
			}
		}

		m_bitmap->Unmap();
	}
#pragma endregion

#pragma region 选择
	winrt::D2DEngine::AreaHandle Layer::HitTestSelectionHandle(D2D1_POINT_2F pt) const
	{
		if (!m_selection.Active) return winrt::D2DEngine::AreaHandle::None;

		D2D1_RECT_F rect = RectConverter::WinRTToD2D(m_selection.Rect);
		float handleSize = m_selection.HandleSize;

		auto Hit = [&](float cx, float cy) {
			return pt.x >= cx - handleSize && pt.x <= cx + handleSize &&
				pt.y >= cy - handleSize && pt.y <= cy + handleSize;
			};

		auto points = RectUtils::GetControlPoints(rect, handleSize);

		if (Hit(points[0].x, points[0].y)) return winrt::D2DEngine::AreaHandle::TopLeft;
		if (Hit(points[1].x, points[1].y)) return winrt::D2DEngine::AreaHandle::TopCenter;
		if (Hit(points[2].x, points[2].y)) return winrt::D2DEngine::AreaHandle::TopRight;
		if (Hit(points[3].x, points[3].y)) return winrt::D2DEngine::AreaHandle::MiddleLeft;
		if (Hit(points[4].x, points[4].y)) return winrt::D2DEngine::AreaHandle::MiddleRight;
		if (Hit(points[5].x, points[5].y)) return winrt::D2DEngine::AreaHandle::BottomLeft;
		if (Hit(points[6].x, points[6].y)) return winrt::D2DEngine::AreaHandle::BottomCenter;
		if (Hit(points[7].x, points[7].y)) return winrt::D2DEngine::AreaHandle::BottomRight;

		if (pt.x > rect.left && pt.x < rect.right &&
			pt.y > rect.top && pt.y < rect.bottom)
			return winrt::D2DEngine::AreaHandle::Move;

		return winrt::D2DEngine::AreaHandle::None;
	}

	// 选择工具调整大小
	void Layer::ResizeSelection(D2D1_POINT_2F newPt, winrt::D2DEngine::AreaHandle handle)
	{
		if (!m_selection.Active || handle == winrt::D2DEngine::AreaHandle::None) return;

		D2D1_RECT_F rect = RectConverter::WinRTToD2D(m_selection.Rect);

		switch (handle)
		{
		case winrt::D2DEngine::AreaHandle::TopLeft:      rect.left = newPt.x; rect.top = newPt.y; break;
		case winrt::D2DEngine::AreaHandle::TopCenter:    rect.top = newPt.y; break;
		case winrt::D2DEngine::AreaHandle::TopRight:     rect.right = newPt.x; rect.top = newPt.y; break;
		case winrt::D2DEngine::AreaHandle::MiddleLeft:   rect.left = newPt.x; break;
		case winrt::D2DEngine::AreaHandle::MiddleRight:  rect.right = newPt.x; break;
		case winrt::D2DEngine::AreaHandle::BottomLeft:   rect.left = newPt.x; rect.bottom = newPt.y; break;
		case winrt::D2DEngine::AreaHandle::BottomCenter: rect.bottom = newPt.y; break;
		case winrt::D2DEngine::AreaHandle::BottomRight:  rect.right = newPt.x; rect.bottom = newPt.y; break;
		case winrt::D2DEngine::AreaHandle::Move:
		{
			float dx = newPt.x - (rect.left + rect.right) / 2.0f;
			float dy = newPt.y - (rect.top + rect.bottom) / 2.0f;
			rect.left += dx; rect.right += dx;
			rect.top += dy; rect.bottom += dy;
		} break;
		default: break;
		}

		// 防止反转
		if (rect.left > rect.right) std::swap(rect.left, rect.right);
		if (rect.top > rect.bottom) std::swap(rect.top, rect.bottom);

		m_selection.Rect = Windows::Foundation::Rect(
			rect.left,
			rect.top,
			rect.right - rect.left,
			rect.bottom - rect.top
		);
	}
#pragma endregion

#pragma region 裁剪
	// 裁剪工具控制点命中测试
	winrt::D2DEngine::AreaHandle Layer::HitTestCropHandle(D2D1_POINT_2F pt) const
	{
		if (!m_crop.Active) return winrt::D2DEngine::AreaHandle::None;

		D2D1_RECT_F rect = RectConverter::WinRTToD2D(m_crop.Rect);
		float handleSize = m_crop.HandleSize;

		auto Hit = [&](float cx, float cy) {
			return pt.x >= cx - handleSize && pt.x <= cx + handleSize &&
				pt.y >= cy - handleSize && pt.y <= cy + handleSize;
			};

		auto points = RectUtils::GetControlPoints(rect, handleSize);

		if (Hit(points[0].x, points[0].y)) return winrt::D2DEngine::AreaHandle::TopLeft;
		if (Hit(points[1].x, points[1].y)) return winrt::D2DEngine::AreaHandle::TopCenter;
		if (Hit(points[2].x, points[2].y)) return winrt::D2DEngine::AreaHandle::TopRight;
		if (Hit(points[3].x, points[3].y)) return winrt::D2DEngine::AreaHandle::MiddleLeft;
		if (Hit(points[4].x, points[4].y)) return winrt::D2DEngine::AreaHandle::MiddleRight;
		if (Hit(points[5].x, points[5].y)) return winrt::D2DEngine::AreaHandle::BottomLeft;
		if (Hit(points[6].x, points[6].y)) return winrt::D2DEngine::AreaHandle::BottomCenter;
		if (Hit(points[7].x, points[7].y)) return winrt::D2DEngine::AreaHandle::BottomRight;

		if (pt.x > rect.left && pt.x < rect.right &&
			pt.y > rect.top && pt.y < rect.bottom)
			return winrt::D2DEngine::AreaHandle::Move;

		return winrt::D2DEngine::AreaHandle::None;
	}

	// 裁剪工具调整大小
	void Layer::ResizeCrop(D2D1_POINT_2F newPt, winrt::D2DEngine::AreaHandle handle)
	{
		if (!m_crop.Active || handle == winrt::D2DEngine::AreaHandle::None) return;

		D2D1_RECT_F rect = RectConverter::WinRTToD2D(m_crop.Rect);

		switch (handle)
		{
		case winrt::D2DEngine::AreaHandle::TopLeft:      rect.left = newPt.x; rect.top = newPt.y; break;
		case winrt::D2DEngine::AreaHandle::TopCenter:    rect.top = newPt.y; break;
		case winrt::D2DEngine::AreaHandle::TopRight:     rect.right = newPt.x; rect.top = newPt.y; break;
		case winrt::D2DEngine::AreaHandle::MiddleLeft:   rect.left = newPt.x; break;
		case winrt::D2DEngine::AreaHandle::MiddleRight:  rect.right = newPt.x; break;
		case winrt::D2DEngine::AreaHandle::BottomLeft:   rect.left = newPt.x; rect.bottom = newPt.y; break;
		case winrt::D2DEngine::AreaHandle::BottomCenter: rect.bottom = newPt.y; break;
		case winrt::D2DEngine::AreaHandle::BottomRight:  rect.right = newPt.x; rect.bottom = newPt.y; break;
		case winrt::D2DEngine::AreaHandle::Move:
		{
			float dx = newPt.x - (rect.left + rect.right) / 2.0f;
			float dy = newPt.y - (rect.top + rect.bottom) / 2.0f;
			rect.left += dx; rect.right += dx;
			rect.top += dy; rect.bottom += dy;
		} break;
		default: break;
		}

		// 防止反转
		if (rect.left > rect.right) std::swap(rect.left, rect.right);
		if (rect.top > rect.bottom) std::swap(rect.top, rect.bottom);

		m_crop.Rect = Windows::Foundation::Rect(
			rect.left,
			rect.top,
			rect.right - rect.left,
			rect.bottom - rect.top
		);
	}
#pragma endregion
}
