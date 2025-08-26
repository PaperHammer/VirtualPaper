#include "pch.h"
#include "Layer.h"
#include "Layer.g.cpp"
#include "ColorUtils.h"
#include "D2DDeviceManager.h"
#include "RectUtils.h"
#include <stack>
#include <PointUtils.h>
#include <ppltasks.h>

namespace winrt::D2DEngine::implementation
{
	Layer::Layer(hstring const& name, int32_t width, int32_t height, winrt::D2DEngine::LayerType type)
		: m_name(name), m_width(width), m_height(height), m_type(type)
	{
		m_ctx = D2DDeviceManager::Instance().CreateIndependentD2DContext();
		m_bitmap = D2DDeviceManager::Instance().CreateRenderTargetForLayer(width, height, m_ctx);
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
	//void Layer::Resize(int32_t newWidth, int32_t newHeight, bool scaleContent)
	//{
	//	if (newWidth == m_width && newHeight == m_height) return;

	//	// 创建新位图
	//	auto newBitmap = D2DDeviceManager::Instance().CreateRenderTargetForLayer(newWidth, newHeight, m_ctx);
	//	if (!newBitmap) {
	//		throw winrt::hresult_error(E_FAIL, L"Failed to create render target");
	//	}

	//	auto ctx = m_ctx.get();
	//	ctx->SetTarget(newBitmap.get());
	//	ctx->BeginDraw();
	//	ctx->Clear(D2D1::ColorF(0, 0, 0, 0));

	//	if (m_bitmap) {
	//		if (scaleContent) {
	//			ctx->DrawBitmap(m_bitmap.get(),
	//				D2D1::RectF(0, 0, (float)newWidth, (float)newHeight),
	//				1.0f, D2D1_BITMAP_INTERPOLATION_MODE_LINEAR);
	//		}
	//		else {
	//			// 非缩放保持左上角内容
	//			D2D1_RECT_F srcRect = D2D1::RectF(
	//				0, 0,
	//				min((float)m_width, (float)newWidth),
	//				min((float)m_height, (float)newHeight));

	//			ctx->DrawBitmap(m_bitmap.get(), srcRect, 1.0f,
	//				D2D1_BITMAP_INTERPOLATION_MODE_NEAREST_NEIGHBOR);
	//		}
	//	}

	//	HRESULT hr = ctx->EndDraw();
	//	if (FAILED(hr)) {
	//		throw winrt::hresult_error(hr, L"Resize draw failed");
	//	}

	//	m_bitmap = newBitmap;
	//	m_width = newWidth;
	//	m_height = newHeight;
	//}
	void Layer::Resize(int32_t newWidth, int32_t newHeight, bool scaleContent)
	{
		if (newWidth == m_width && newHeight == m_height) return;

		auto newBitmap = D2DDeviceManager::Instance().CreateRenderTargetForLayer(newWidth, newHeight, m_ctx);
		if (!newBitmap) {
			throw winrt::hresult_error(E_FAIL, L"Failed to create render target");
		}

		// 使用独立上下文，避免资源冲突
		auto tempCtx = D2DDeviceManager::Instance().CreateIndependentD2DContext();
		if (!tempCtx) {
			throw winrt::hresult_error(E_FAIL, L"Failed to create temp context");
		}

		tempCtx->SetTarget(newBitmap.get());
		tempCtx->BeginDraw();
		tempCtx->Clear(D2D1::ColorF(0, 0, 0, 0));

		if (m_bitmap) {
			if (scaleContent) {
				tempCtx->DrawBitmap(
					m_bitmap.get(),
					D2D1::RectF(0, 0, (float)newWidth, (float)newHeight),
					1.0f,
					D2D1_BITMAP_INTERPOLATION_MODE_LINEAR
				);
			}
			else {
				D2D1_RECT_F srcRect = D2D1::RectF(
					0, 0,
					min((float)m_width, (float)newWidth),
					min((float)m_height, (float)newHeight)
				);
				tempCtx->DrawBitmap(
					m_bitmap.get(),
					srcRect,
					1.0f,
					D2D1_BITMAP_INTERPOLATION_MODE_NEAREST_NEIGHBOR
				);
			}
		}

		HRESULT hr = tempCtx->EndDraw();
		if (FAILED(hr)) {
			throw winrt::hresult_error(hr, L"Resize draw failed");
		}

		m_bitmap = newBitmap;
		m_width = newWidth;
		m_height = newHeight;
	}

	void Layer::Rotate(winrt::D2DEngine::RotateDirection direction)
	{
		double angle = (direction == winrt::D2DEngine::RotateDirection::Right) ? DEFAULT_ROTATION_ANGLE : -DEFAULT_ROTATION_ANGLE;
		m_rotation = fmod(m_rotation + angle, 360.0);
		if (m_rotation < 0) m_rotation += 360.0;

		if (m_type == winrt::D2DEngine::LayerType::Bitmap && m_bitmap)
		{
			int newWidth = m_height;
			int newHeight = m_width;

			auto rotatedBitmap = D2DDeviceManager::Instance().CreateRenderTargetForLayer(newWidth, newHeight, m_ctx);
			if (!rotatedBitmap) return;

			auto ctx = m_ctx.get();
			ctx->SetTarget(rotatedBitmap.get());
			ctx->BeginDraw();
			ctx->Clear(D2D1::ColorF(0, 0, 0, 0));

			// 设置旋转变换
			D2D1_POINT_2F center = D2D1::Point2F(newWidth / 2.0f, newHeight / 2.0f);
			ctx->SetTransform(D2D1::Matrix3x2F::Rotation(static_cast<float>(angle), center));

			// 绘制原图（居中）
			float x = (newWidth - m_width) / 2.0f;
			float y = (newHeight - m_height) / 2.0f;
			ctx->DrawBitmap(
				m_bitmap.get(),
				D2D1::RectF(x, y, x + m_width, y + m_height)
			);

			ctx->SetTransform(D2D1::Matrix3x2F::Identity()); // 重置变换
			ctx->EndDraw();

			// 更新图层
			m_bitmap = rotatedBitmap;
			m_width = newWidth;
			m_height = newHeight;
		}
	}

	void Layer::FlipHorizontal()
	{
		m_flipH = !m_flipH;

		if (m_type == winrt::D2DEngine::LayerType::Bitmap && m_bitmap)
		{
			auto ctx = m_ctx.get();
			ctx->SetTarget(m_bitmap.get());
			ctx->BeginDraw();
			ctx->Clear(D2D1::ColorF(0, 0, 0, 0));

			// 水平翻转：X 轴缩放 -1，中心为图像中心
			ctx->SetTransform(D2D1::Matrix3x2F::Scale(
				-1.0f, 1.0f,
				D2D1::Point2F(m_width / 2.0f, m_height / 2.0f)
			));

			ctx->DrawBitmap(m_bitmap.get());

			ctx->SetTransform(D2D1::Matrix3x2F::Identity());
			ctx->EndDraw();

			// 们直接在原位图上绘制，所以内容已被翻转
			// m_bitmap 不变，但像素内容已翻转
		}
	}
	void Layer::FlipVertical()
	{
		m_flipV = !m_flipV;

		if (m_type == winrt::D2DEngine::LayerType::Bitmap && m_bitmap)
		{
			auto ctx = m_ctx.get();
			ctx->SetTarget(m_bitmap.get());
			ctx->BeginDraw();
			ctx->Clear(D2D1::ColorF(0, 0, 0, 0));

			// 垂直翻转
			ctx->SetTransform(D2D1::Matrix3x2F::Scale(
				1.0f, -1.0f,
				D2D1::Point2F(m_width / 2.0f, m_height / 2.0f)
			));

			ctx->DrawBitmap(m_bitmap.get());

			ctx->SetTransform(D2D1::Matrix3x2F::Identity());
			ctx->EndDraw();
		}
	}
#pragma endregion

#pragma region 渲染
	winrt::Windows::Foundation::IAsyncOperation<winrt::Windows::Storage::Streams::IBuffer> Layer::RenderToBufferAsync()
	{
		uint32_t width = m_width;
		uint32_t height = m_height;
		if (!m_bitmap) co_return nullptr;

		uint32_t stride = width * 4; // 32位BGRA格式
		D2D1_MAPPED_RECT mapped = {};
		HRESULT hr = m_bitmap->Map(D2D1_MAP_OPTIONS_READ, &mapped);
		if (FAILED(hr)) co_return nullptr;

		uint32_t bufferSize = stride * height;
		winrt::Windows::Storage::Streams::Buffer buffer(bufferSize);

		// 直接获取 buffer 的数据指针
		uint8_t* data = buffer.data();
		if (mapped.pitch == stride) {
			// 内存连续，直接一次性拷贝
			memcpy(data, mapped.bits, bufferSize);
		}
		else {
			// 行间有填充，逐行拷贝
			for (uint32_t y = 0; y < height; ++y) {
				memcpy(data + y * stride, mapped.bits + y * mapped.pitch, stride);
			}
		}

		m_bitmap->Unmap();
		co_return buffer;
	}

	winrt::com_ptr<ID2D1Bitmap1> Layer::GetBitmap()
	{
		return m_bitmap;
	}

	winrt::com_ptr<ID2D1DeviceContext5> Layer::GetContext()
	{
		//auto ctx = D2DDeviceManager::Instance().GetD2DContext();
		//ctx->SetTarget(m_bitmap.Get());
		return m_ctx;
	}
#pragma endregion

#pragma region 绘图	
	void Layer::DrawStroke(
		winrt::Windows::Foundation::Collections::IVector<winrt::Windows::Foundation::Point> const& points,
		winrt::D2DEngine::StrokeOptions const& options)
	{
		if (!m_bitmap || points.Size() < 1) return;

		auto d2dPoints = PointUtils::ToD2DPoints(points);
		m_ctx->SetTarget(m_bitmap.get());
		m_ctx->BeginDraw();

		// 创建画笔
		winrt::com_ptr<ID2D1SolidColorBrush> brush;
		D2D1_COLOR_F color = ColorConverter::ToD2DColor(options.Color);
		if (options.Tool == winrt::D2DEngine::StrokeTool::Eraser) {
			color = D2D1::ColorF(0, 0);
		}

		winrt::check_hresult(
			m_ctx->CreateSolidColorBrush(
				color,
				brush.put()
			)
		);

		if (points.Size() == 1) {
			const float radius = options.Thickness / 2.0f;
			const auto& center = d2dPoints[0];
			m_ctx->FillEllipse(
				D2D1::Ellipse(center, radius, radius),
				brush.get());
		}
		else {
			auto smoothPoints = GenerateSmoothCurve(d2dPoints);

			// 创建路径几何
			winrt::com_ptr<ID2D1PathGeometry> geometry;
			winrt::check_hresult(
				D2DDeviceManager::Instance().GetD2DFactory()->CreatePathGeometry(
					geometry.put()
				)
			);

			winrt::com_ptr<ID2D1GeometrySink> sink;
			winrt::check_hresult(
				geometry->Open(sink.put())
			);

			sink->BeginFigure(smoothPoints[0], D2D1_FIGURE_BEGIN_HOLLOW);
			for (size_t i = 1; i < smoothPoints.size(); ++i) {
				sink->AddLine(smoothPoints[i]);
			}
			sink->EndFigure(D2D1_FIGURE_END_OPEN);
			sink->Close();

			// 笔触样式
			winrt::com_ptr<ID2D1StrokeStyle> strokeStyle;
			if (options.Tool == winrt::D2DEngine::StrokeTool::Eraser) {
				D2D1_STROKE_STYLE_PROPERTIES props = {};
				props.dashStyle = D2D1_DASH_STYLE_DASH;
				winrt::check_hresult(
					D2DDeviceManager::Instance().GetD2DFactory()->CreateStrokeStyle(
						&props,
						nullptr,
						0,
						strokeStyle.put()
					)
				);
			}

			m_ctx->DrawGeometry(
				geometry.get(),
				brush.get(),
				options.Thickness,
				strokeStyle.get());
		}

		m_ctx->EndDraw();
	}

	// 辅助方法：生成平滑曲线
	vector<D2D1_POINT_2F> Layer::GenerateSmoothCurve(const vector<D2D1_POINT_2F>& points)
	{
		vector<D2D1_POINT_2F> smoothPoints;
		if (points.size() < 2) return points;

		smoothPoints.push_back(points[0]);
		for (size_t i = 1; i + 2 < points.size(); ++i) {
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
		return smoothPoints;
	}

	void Layer::CreatePathGeometry(const vector<D2D1_POINT_2F>& points, ID2D1PathGeometry** outGeometry)
	{
		winrt::com_ptr<ID2D1PathGeometry> geometry;
		winrt::check_hresult(D2DDeviceManager::Instance().GetD2DFactory()->CreatePathGeometry(geometry.put()));

		winrt::com_ptr<ID2D1GeometrySink> sink;
		winrt::check_hresult(geometry->Open(sink.put()));

		sink->BeginFigure(points[0], D2D1_FIGURE_BEGIN_HOLLOW);
		for (size_t i = 1; i < points.size(); ++i) {
			sink->AddLine(points[i]);
		}
		sink->EndFigure(D2D1_FIGURE_END_OPEN);
		sink->Close();

		*outGeometry = geometry.detach(); // 仅在不使用com_ptr时使用
	}

	// 辅助方法：创建橡皮擦笔触样式
	void Layer::CreateEraserStrokeStyle(ID2D1StrokeStyle** style)
	{
		D2D1_STROKE_STYLE_PROPERTIES props = {};
		props.dashStyle = D2D1_DASH_STYLE_DASH;
		D2DDeviceManager::Instance().GetD2DFactory()
			->CreateStrokeStyle(&props, nullptr, 0, style);
	}

	void Layer::FillRect(
		winrt::Windows::Foundation::Rect const& rect,
		winrt::D2DEngine::FillOptions const& options)
	{
		if (!m_bitmap) return;

		auto rectF = RectConverter::ToD2DRect(rect);
		m_ctx->SetTarget(m_bitmap.get());
		m_ctx->BeginDraw();

		// 创建画刷
		winrt::com_ptr<ID2D1SolidColorBrush> brush;
		D2D1_COLOR_F color = {
			options.Color.x,
			options.Color.y,
			options.Color.z,
			options.Color.w * options.Opacity
		};

		winrt::check_hresult(m_ctx->CreateSolidColorBrush(color, brush.put()));

		// 填充矩形
		m_ctx->FillRectangle(rectF, brush.get());
		m_ctx->EndDraw();
	}

	void Layer::FillPath(
		winrt::Windows::Foundation::Collections::IVector<winrt::Windows::Foundation::Point> const& points,
		winrt::D2DEngine::FillOptions const& options)
	{
		if (!m_bitmap || points.Size() < 3) return;

		auto d2dPoints = PointUtils::ToD2DPoints(points);
		m_ctx->SetTarget(m_bitmap.get());
		m_ctx->BeginDraw();

		// 创建路径几何
		winrt::com_ptr<ID2D1PathGeometry> path;
		winrt::check_hresult(
			D2DDeviceManager::Instance().GetD2DFactory()->CreatePathGeometry(path.put())
		);

		// 2. 打开几何接收器
		winrt::com_ptr<ID2D1GeometrySink> sink;
		winrt::check_hresult(path->Open(sink.put())
		);

		// 绘制路径
		sink->BeginFigure(d2dPoints[0], D2D1_FIGURE_BEGIN_FILLED);
		for (size_t i = 1; i < d2dPoints.size(); ++i) {
			sink->AddLine(d2dPoints[i]);
		}
		sink->EndFigure(D2D1_FIGURE_END_CLOSED);
		sink->Close();

		// 创建画刷（正确的CreateSolidColorBrush调用）
		winrt::com_ptr<ID2D1SolidColorBrush> brush;
		D2D1_COLOR_F color = {
			options.Color.x,
			options.Color.y,
			options.Color.z,
			options.Color.w * options.Opacity
		};

		winrt::check_hresult(m_ctx->CreateSolidColorBrush(color, brush.put()));

		// 填充路径
		m_ctx->FillGeometry(path.get(), brush.get());
		m_ctx->EndDraw();
	}

	void Layer::FloodFill(int x, int y, winrt::D2DEngine::FillOptions const& options)
	{
		if (!m_bitmap) return;

		// Map bitmap
		D2D1_MAPPED_RECT mapped;
		HRESULT hr = m_bitmap->Map(D2D1_MAP_OPTIONS_READ | D2D1_MAP_OPTIONS_WRITE, &mapped);
		if (FAILED(hr)) return;

		auto width = m_width;
		auto height = m_height;
		auto pixels = (uint32_t*)mapped.bits;

		// 获取起点颜色
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

		if (ColorUtils::ColorWithinTolerance(targetColor, ColorConverter::ToD2DColor(options.Color), options.Tolerance))
		{
			m_bitmap->Unmap();
			return; // 相同颜色无需填充
		}

		// 扫描线算法
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
	winrt::D2DEngine::AreaHandle Layer::HitTestSelectionHandle(winrt::Windows::Foundation::Point pt) const
	{
		if (!m_selection.Active) return winrt::D2DEngine::AreaHandle::None;

		auto point = PointUtils::ToD2DPoint(pt);

		D2D1_RECT_F rect = RectConverter::ToD2DRect(m_selection.Rect);
		float handleSize = m_selection.HandleSize;

		auto Hit = [&](float cx, float cy) {
			return point.x >= cx - handleSize && point.x <= cx + handleSize &&
				point.y >= cy - handleSize && point.y <= cy + handleSize;
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

		if (point.x > rect.left && point.x < rect.right &&
			point.y > rect.top && point.y < rect.bottom)
			return winrt::D2DEngine::AreaHandle::Move;

		return winrt::D2DEngine::AreaHandle::None;
	}

	// 选择工具调整大小
	void Layer::ResizeSelection(winrt::Windows::Foundation::Point newPt, winrt::D2DEngine::AreaHandle handle)
	{
		if (!m_selection.Active || handle == winrt::D2DEngine::AreaHandle::None) return;

		auto newPoint = PointUtils::ToD2DPoint(newPt);
		D2D1_RECT_F rect = RectConverter::ToD2DRect(m_selection.Rect);

		switch (handle)
		{
		case winrt::D2DEngine::AreaHandle::TopLeft:      rect.left = newPoint.x; rect.top = newPoint.y; break;
		case winrt::D2DEngine::AreaHandle::TopCenter:    rect.top = newPoint.y; break;
		case winrt::D2DEngine::AreaHandle::TopRight:     rect.right = newPoint.x; rect.top = newPoint.y; break;
		case winrt::D2DEngine::AreaHandle::MiddleLeft:   rect.left = newPoint.x; break;
		case winrt::D2DEngine::AreaHandle::MiddleRight:  rect.right = newPoint.x; break;
		case winrt::D2DEngine::AreaHandle::BottomLeft:   rect.left = newPoint.x; rect.bottom = newPoint.y; break;
		case winrt::D2DEngine::AreaHandle::BottomCenter: rect.bottom = newPoint.y; break;
		case winrt::D2DEngine::AreaHandle::BottomRight:  rect.right = newPoint.x; rect.bottom = newPoint.y; break;
		case winrt::D2DEngine::AreaHandle::Move:
		{
			float dx = newPoint.x - (rect.left + rect.right) / 2.0f;
			float dy = newPoint.y - (rect.top + rect.bottom) / 2.0f;
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
	winrt::D2DEngine::AreaHandle Layer::HitTestCropHandle(winrt::Windows::Foundation::Point pt) const
	{
		if (!m_crop.Active) return winrt::D2DEngine::AreaHandle::None;

		auto point = PointUtils::ToD2DPoint(pt);
		D2D1_RECT_F rect = RectConverter::ToD2DRect(m_crop.Rect);
		float handleSize = m_crop.HandleSize;

		auto Hit = [&](float cx, float cy) {
			return point.x >= cx - handleSize && point.x <= cx + handleSize &&
				point.y >= cy - handleSize && point.y <= cy + handleSize;
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

		if (point.x > rect.left && point.x < rect.right &&
			point.y > rect.top && point.y < rect.bottom)
			return winrt::D2DEngine::AreaHandle::Move;

		return winrt::D2DEngine::AreaHandle::None;
	}

	// 裁剪工具调整大小
	void Layer::ResizeCrop(winrt::Windows::Foundation::Point newPt, winrt::D2DEngine::AreaHandle handle)
	{
		if (!m_crop.Active || handle == winrt::D2DEngine::AreaHandle::None) return;

		auto newPoint = PointUtils::ToD2DPoint(newPt);
		D2D1_RECT_F rect = RectConverter::ToD2DRect(m_crop.Rect);

		switch (handle)
		{
		case winrt::D2DEngine::AreaHandle::TopLeft:      rect.left = newPoint.x; rect.top = newPoint.y; break;
		case winrt::D2DEngine::AreaHandle::TopCenter:    rect.top = newPoint.y; break;
		case winrt::D2DEngine::AreaHandle::TopRight:     rect.right = newPoint.x; rect.top = newPoint.y; break;
		case winrt::D2DEngine::AreaHandle::MiddleLeft:   rect.left = newPoint.x; break;
		case winrt::D2DEngine::AreaHandle::MiddleRight:  rect.right = newPoint.x; break;
		case winrt::D2DEngine::AreaHandle::BottomLeft:   rect.left = newPoint.x; rect.bottom = newPoint.y; break;
		case winrt::D2DEngine::AreaHandle::BottomCenter: rect.bottom = newPoint.y; break;
		case winrt::D2DEngine::AreaHandle::BottomRight:  rect.right = newPoint.x; rect.bottom = newPoint.y; break;
		case winrt::D2DEngine::AreaHandle::Move:
		{
			float dx = newPoint.x - (rect.left + rect.right) / 2.0f;
			float dy = newPoint.y - (rect.top + rect.bottom) / 2.0f;
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
