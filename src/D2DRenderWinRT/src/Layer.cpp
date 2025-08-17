#include "pch.h"
#include "Layer.h"
#include <stack>
#include <cmath>

namespace winrt::D2DEngine::implementation
{
    Layer::Layer(hstring const& name, int32_t width, int32_t height, D2DEngine::LayerType type)
        : m_name(name), m_width(width), m_height(height), m_type(type)
    {
        m_bitmap = D2DDeviceManager::Instance().CreateRenderTargetForLayer(width, height);
    }

    hstring Layer::Name() { return m_name; }
    void Layer::Name(hstring const& value) { m_name = value; }

    int32_t Layer::Width() { return m_width; }
    int32_t Layer::Height() { return m_height; }

    D2DEngine::LayerType Layer::Type() { return m_type; }

    bool Layer::Visible() { return m_visible; }
    void Layer::Visible(bool value) { m_visible = value; }

    double Layer::Opacity() { return m_opacity; }
    void Layer::Opacity(double value) { m_opacity = std::clamp(value, 0.0, 1.0); }

    void Layer::Resize(int32_t newWidth, int32_t newHeight)
    {
        m_width = newWidth;
        m_height = newHeight;
        m_bitmap = D2DDeviceManager::Instance().CreateRenderTargetForLayer(newWidth, newHeight);
    }

    void Layer::Rotate(double /*angleDegrees*/) {}
    void Layer::FlipHorizontal() {}
    void Layer::FlipVertical() {}

    Microsoft::WRL::ComPtr<ID2D1Bitmap1> Layer::GetBitmap()
    {
        return m_bitmap;
    }

    Microsoft::WRL::ComPtr<ID2D1DeviceContext5> Layer::GetContext()
    {
        auto ctx = D2DDeviceManager::Instance().GetD2DContext();
        ctx->SetTarget(m_bitmap.Get());
        return ctx;
    }

    void Layer::DrawStroke(std::vector<D2D1_POINT_2F> const& points, StrokeOptions const& options)
    {
        if (!m_bitmap || points.size() < 2)
            return;

        auto ctx = D2DDeviceManager::Instance().GetD2DContext();
        ctx->SetTarget(m_bitmap.Get());
        ctx->BeginDraw();

        // 创建画笔
        Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> brush;
        D2D1_COLOR_F color = options.Color;
        if (options.Tool == StrokeTool::Eraser)
        {
            // 橡皮擦直接清空像素，使用透明
            color = D2D1::ColorF(0, 0); // alpha = 0
        }

        ctx->CreateSolidColorBrush(color, &brush);

        // 平滑曲线：使用 Catmull-Rom 样条插值
        std::vector<D2D1_POINT_2F> smoothPoints;
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

        // 绘制路径
        Microsoft::WRL::ComPtr<ID2D1PathGeometry> geometry;
        D2DDeviceManager::Instance().GetFactory()->CreatePathGeometry(&geometry);

        Microsoft::WRL::ComPtr<ID2D1GeometrySink> sink;
        geometry->Open(&sink);
        sink->BeginFigure(smoothPoints[0], D2D1_FIGURE_BEGIN_HOLLOW);
        for (size_t i = 1; i < smoothPoints.size(); ++i)
        {
            sink->AddLine(smoothPoints[i]);
        }
        sink->EndFigure(D2D1_FIGURE_END_OPEN);
        sink->Close();

        // 设置透明度
        float opacity = options.Opacity;
        ctx->DrawGeometry(geometry.Get(), brush.Get(), options.Thickness, nullptr, opacity);

        ctx->EndDraw();
    }

    inline bool ColorWithinTolerance(D2D1_COLOR_F a, D2D1_COLOR_F b, float tolerance)
    {
        float dr = a.r - b.r;
        float dg = a.g - b.g;
        float db = a.b - b.b;
        float da = a.a - b.a;
        float dist = std::sqrt(dr * dr + dg * dg + db * db + da * da);
        return dist <= tolerance;
    }

    void Layer::FloodFill(int x, int y, FillOptions const& options)
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
        uint32_t fillPixel = FillColorToPixel(D2D1::ColorF(options.Color.r, options.Color.g, options.Color.b, options.Opacity));

        if (ColorWithinTolerance(targetColor, options.Color, options.Tolerance))
        {
            m_bitmap->Unmap();
            return; // 相同颜色无需填充
        }

        // 3. 扫描线算法
        struct Span { int x1, x2, int y; };
        std::stack<Span> stack;
        stack.push({ x, x, y });

        while (!stack.empty())
        {
            Span s = stack.top(); stack.pop();
            int left = s.x1;
            int right = s.x2;
            int row = s.y;

            // 向左扩展
            while (left >= 0 && ColorWithinTolerance(PixelToColor(pixels[row * width + left]), targetColor, options.Tolerance))
                --left;
            ++left;

            // 向右扩展
            while (right < width && ColorWithinTolerance(PixelToColor(pixels[row * width + right]), targetColor, options.Tolerance))
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
                    while (nx <= right && ColorWithinTolerance(PixelToColor(pixels[ny * width + nx]), targetColor, options.Tolerance))
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

    void Layer::CaptureSelection(D2D1_RECT_F const& rect)
    {
        if (!m_bitmap) return;
        m_selection.Rect = rect;
        m_selection.OriginalRect = rect;
        m_selection.Active = true;

        // 创建临时 bitmap
        D2D1_BITMAP_PROPERTIES1 props = {};
        props.pixelFormat.format = DXGI_FORMAT_B8G8R8A8_UNORM;
        props.pixelFormat.alphaMode = D2D1_ALPHA_MODE_PREMULTIPLIED;
        props.dpiX = props.dpiY = 96.0f;
        props.bitmapOptions = D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS_CANNOT_DRAW;

        auto ctx = D2DDeviceManager::Instance().GetD2DContext();
        Microsoft::WRL::ComPtr<ID2D1Bitmap1> tmpBitmap;
        ctx->CreateBitmap(
            D2D1_SIZE_U{ (UINT32)rect.right - (UINT32)rect.left, (UINT32)rect.bottom - (UINT32)rect.top },
            nullptr, 0, &props, &tmpBitmap);

        // 将选区内容复制到 tmpBitmap
        D2D1_RECT_U srcRect{ (UINT32)rect.left, (UINT32)rect.top, (UINT32)rect.right, (UINT32)rect.bottom };
        ctx->CopyFromBitmap(tmpBitmap.Get(), m_bitmap.Get(), &srcRect, D2D1_POINT_2U{ 0,0 });

        m_selection.Bitmap = tmpBitmap;
    }

    void Layer::MoveSelection(D2D1_POINT_2F delta)
    {
        if (!m_selection.Active) return;
        m_selection.Rect.left += delta.x;
        m_selection.Rect.right += delta.x;
        m_selection.Rect.top += delta.y;
        m_selection.Rect.bottom += delta.y;
    }

    void Layer::RenderWithSelection(ID2D1DeviceContext5* ctx)
    {
        ctx->SetTarget(m_bitmap.Get());
        ctx->BeginDraw();

        // 绘制原始内容
        ctx->DrawBitmap(m_bitmap.Get());

        // 绘制选区临时 bitmap
        if (m_selection.Active && m_selection.Bitmap)
        {
            ctx->DrawBitmap(
                m_selection.Bitmap.Get(),
                &m_selection.Rect,
                1.0f,
                D2D1_BITMAP_INTERPOLATION_MODE_LINEAR
            );

            // 绘制虚线框
            Microsoft::WRL::ComPtr<ID2D1StrokeStyle> stroke;
            D2D1_STROKE_STYLE_PROPERTIES props = {};
            props.dashStyle = D2D1_DASH_STYLE_DASH;
            D2DDeviceManager::Instance().GetFactory()->CreateStrokeStyle(&props, nullptr, 0, &stroke);

            Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> brush;
            ctx->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::Black), &brush);

            ctx->DrawRectangle(m_selection.Rect, brush.Get(), 1.0f, stroke.Get());
        }

        ctx->EndDraw();
    }

    void Layer::PlaceSelection()
    {
        if (!m_selection.Active || !m_selection.Bitmap) return;

        auto ctx = D2DDeviceManager::Instance().GetD2DContext();
        ctx->SetTarget(m_bitmap.Get());
        ctx->BeginDraw();

        ctx->DrawBitmap(m_selection.Bitmap.Get(), &m_selection.Rect);

        ctx->EndDraw();

        m_selection.Active = false; // 放置后清除选区
    }

    void Layer::CancelSelection()
    {
        if (!m_selection.Active || !m_selection.Bitmap) return;

        m_selection.Rect = m_selection.OriginalRect;
        // 不需要修改 Layer bitmap，原始内容保持不变
        m_selection.Active = false;
    }

    SelectionHandle Layer::HitTestSelectionHandle(D2D1_POINT_2F pt)
    {
        if (!m_selection.Active) return SelectionHandle::None;

        auto r = m_selection.Rect;
        float s = m_selection.HandleSize;

        auto Hit = [&](float cx, float cy) {
            return pt.x >= cx - s && pt.x <= cx + s && pt.y >= cy - s && pt.y <= cy + s;
            };

        if (Hit(r.left, r.top)) return SelectionHandle::TopLeft;
        if (Hit((r.left + r.right) / 2, r.top)) return SelectionHandle::TopCenter;
        if (Hit(r.right, r.top)) return SelectionHandle::TopRight;
        if (Hit(r.left, (r.top + r.bottom) / 2)) return SelectionHandle::MiddleLeft;
        if (Hit(r.right, (r.top + r.bottom) / 2)) return SelectionHandle::MiddleRight;
        if (Hit(r.left, r.bottom)) return SelectionHandle::BottomLeft;
        if (Hit((r.left + r.right) / 2, r.bottom)) return SelectionHandle::BottomCenter;
        if (Hit(r.right, r.bottom)) return SelectionHandle::BottomRight;

        // 检查是否在选区内部可移动
        if (pt.x > r.left && pt.x < r.right && pt.y > r.top && pt.y < r.bottom)
            return SelectionHandle::Move;

        return SelectionHandle::None;
    }

    void Layer::RenderWithSelection(ID2D1DeviceContext5* ctx)
    {
        ctx->SetTarget(m_bitmap.Get());
        ctx->BeginDraw();

        // 绘制原始内容
        ctx->DrawBitmap(m_bitmap.Get());

        if (m_selection.Active && m_selection.Bitmap)
        {
            // 绘制选区 bitmap
            ctx->DrawBitmap(m_selection.Bitmap.Get(), &m_selection.Rect);

            // 绘制虚线框
            Microsoft::WRL::ComPtr<ID2D1StrokeStyle> stroke;
            D2D1_STROKE_STYLE_PROPERTIES props = {};
            props.dashStyle = D2D1_DASH_STYLE_DASH;
            D2DDeviceManager::Instance().GetFactory()->CreateStrokeStyle(&props, nullptr, 0, &stroke);

            Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> brush;
            ctx->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::Black), &brush);
            ctx->DrawRectangle(m_selection.Rect, brush.Get(), 1.0f, stroke.Get());

            // 绘制控制点
            float s = m_selection.HandleSize;
            std::vector<D2D1_POINT_2F> handles = {
                {m_selection.Rect.left, m_selection.Rect.top},
                {(m_selection.Rect.left + m_selection.Rect.right) / 2, m_selection.Rect.top},
                {m_selection.Rect.right, m_selection.Rect.top},
                {m_selection.Rect.left, (m_selection.Rect.top + m_selection.Rect.bottom) / 2},
                {m_selection.Rect.right, (m_selection.Rect.top + m_selection.Rect.bottom) / 2},
                {m_selection.Rect.left, m_selection.Rect.bottom},
                {(m_selection.Rect.left + m_selection.Rect.right) / 2, m_selection.Rect.bottom},
                {m_selection.Rect.right, m_selection.Rect.bottom}
            };

            for (auto& pt : handles)
            {
                D2D1_RECT_F handleRect = { pt.x - s, pt.y - s, pt.x + s, pt.y + s };
                ctx->FillRectangle(handleRect, brush.Get());
            }
        }

        ctx->EndDraw();
    }

    void Layer::ResizeSelection(D2D1_POINT_2F newPt, SelectionHandle handle)
    {
        if (!m_selection.Active || handle == SelectionHandle::None) return;
        auto r = m_selection.Rect;

        switch (handle)
        {
        case SelectionHandle::TopLeft:
            r.left = newPt.x; r.top = newPt.y; break;
        case SelectionHandle::TopCenter:
            r.top = newPt.y; break;
        case SelectionHandle::TopRight:
            r.right = newPt.x; r.top = newPt.y; break;
        case SelectionHandle::MiddleLeft:
            r.left = newPt.x; break;
        case SelectionHandle::MiddleRight:
            r.right = newPt.x; break;
        case SelectionHandle::BottomLeft:
            r.left = newPt.x; r.bottom = newPt.y; break;
        case SelectionHandle::BottomCenter:
            r.bottom = newPt.y; break;
        case SelectionHandle::BottomRight:
            r.right = newPt.x; r.bottom = newPt.y; break;
        default: break;
        }

        // 确保左右上下不会反转
        if (r.left > r.right) std::swap(r.left, r.right);
        if (r.top > r.bottom) std::swap(r.top, r.bottom);

        m_selection.Rect = r;
    }

    void Layer::StartCrop(D2D1_POINT_2F startPoint)
    {
        m_crop.Active = true;
        m_crop.Rect.left = startPoint.x;
        m_crop.Rect.top = startPoint.y;
        m_crop.Rect.right = startPoint.x;
        m_crop.Rect.bottom = startPoint.y;
    }

    void Layer::UpdateCrop(D2D1_POINT_2F currentPoint)
    {
        if (!m_crop.Active) return;

        float left = std::min(m_crop.Rect.left, currentPoint.x);
        float top = std::min(m_crop.Rect.top, currentPoint.y);
        float right = std::max(m_crop.Rect.left, currentPoint.x);
        float bottom = std::max(m_crop.Rect.top, currentPoint.y);

        m_crop.Rect = D2D1::RectF(left, top, right, bottom);
    }

    void Layer::RenderCrop(ID2D1DeviceContext5* ctx)
    {
        if (!m_crop.Active) return;

        ctx->SetTarget(m_bitmap.Get());
        ctx->BeginDraw();
        ctx->DrawBitmap(m_bitmap.Get());

        // 创建半透明灰色刷子
        Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> grayBrush;
        ctx->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::Black, m_crop.OverlayOpacity), &grayBrush);

        float w = static_cast<float>(m_width);
        float h = static_cast<float>(m_height);
        D2D1_RECT_F r = m_crop.Rect;

        // 绘制蒙层
        ctx->FillRectangle(D2D1::RectF(0, 0, r.left, h), grayBrush.Get());
        ctx->FillRectangle(D2D1::RectF(r.right, 0, w, h), grayBrush.Get());
        ctx->FillRectangle(D2D1::RectF(r.left, 0, r.right, r.top), grayBrush.Get());
        ctx->FillRectangle(D2D1::RectF(r.left, r.bottom, r.right, h), grayBrush.Get());

        // 绘制虚线框
        Microsoft::WRL::ComPtr<ID2D1StrokeStyle> strokeStyle;
        D2D1_STROKE_STYLE_PROPERTIES props{};
        props.dashStyle = D2D1_DASH_STYLE_DASH;
        D2DDeviceManager::Instance().GetFactory()->CreateStrokeStyle(&props, nullptr, 0, &strokeStyle);

        Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> borderBrush;
        ctx->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::White), &borderBrush);
        ctx->DrawRectangle(r, borderBrush.Get(), 1.0f, strokeStyle.Get());

        // 绘制控制点
        std::vector<D2D1_POINT_2F> handles = {
            {r.left, r.top},
            {(r.left + r.right) / 2, r.top},
            {r.right, r.top},
            {r.left, (r.top + r.bottom) / 2},
            {r.right, (r.top + r.bottom) / 2},
            {r.left, r.bottom},
            {(r.left + r.right) / 2, r.bottom},
            {r.right, r.bottom}
        };

        for (auto& pt : handles)
        {
            D2D1_RECT_F handleRect = { pt.x - m_cropHandleSize, pt.y - m_cropHandleSize,
                                       pt.x + m_cropHandleSize, pt.y + m_cropHandleSize };
            ctx->FillRectangle(handleRect, borderBrush.Get());
        }

        ctx->EndDraw();
    }

    CropHandle::Type Layer::HitTestCropHandle(D2D1_POINT_2F pt)
    {
        if (!m_crop.Active) return CropHandle::None;

        auto r = m_crop.Rect;
        float s = m_cropHandleSize;

        auto Hit = [&](float cx, float cy) {
            return pt.x >= cx - s && pt.x <= cx + s && pt.y >= cy - s && pt.y <= cy + s;
            };

        if (Hit(r.left, r.top)) return CropHandle::TopLeft;
        if (Hit((r.left + r.right) / 2, r.top)) return CropHandle::TopCenter;
        if (Hit(r.right, r.top)) return CropHandle::TopRight;
        if (Hit(r.left, (r.top + r.bottom) / 2)) return CropHandle::MiddleLeft;
        if (Hit(r.right, (r.top + r.bottom) / 2)) return CropHandle::MiddleRight;
        if (Hit(r.left, r.bottom)) return CropHandle::BottomLeft;
        if (Hit((r.left + r.right) / 2, r.bottom)) return CropHandle::BottomCenter;
        if (Hit(r.right, r.bottom)) return CropHandle::BottomRight;

        // 检查是否在框内部可移动
        if (pt.x > r.left && pt.x < r.right && pt.y > r.top && pt.y < r.bottom)
            return CropHandle::Move;

        return CropHandle::None;
    }

    void Layer::ResizeCrop(D2D1_POINT_2F newPt, CropHandle::Type handle)
    {
        if (!m_crop.Active || handle == CropHandle::None) return;

        auto r = m_crop.Rect;

        switch (handle)
        {
        case CropHandle::TopLeft:      r.left = newPt.x; r.top = newPt.y; break;
        case CropHandle::TopCenter:    r.top = newPt.y; break;
        case CropHandle::TopRight:     r.right = newPt.x; r.top = newPt.y; break;
        case CropHandle::MiddleLeft:   r.left = newPt.x; break;
        case CropHandle::MiddleRight:  r.right = newPt.x; break;
        case CropHandle::BottomLeft:   r.left = newPt.x; r.bottom = newPt.y; break;
        case CropHandle::BottomCenter: r.bottom = newPt.y; break;
        case CropHandle::BottomRight:  r.right = newPt.x; r.bottom = newPt.y; break;
        case CropHandle::Move:
        {
            float dx = newPt.x - (r.left + r.right) / 2;
            float dy = newPt.y - (r.top + r.bottom) / 2;
            r.left += dx; r.right += dx;
            r.top += dy; r.bottom += dy;
        } break;
        default: break;
        }

        // 防止反转
        if (r.left > r.right) std::swap(r.left, r.right);
        if (r.top > r.bottom) std::swap(r.top, r.bottom);

        m_crop.Rect = r;
    }

    void Layer::ResizeLayer(int32_t newWidth, int32_t newHeight)
    {
        if (!m_bitmap) return;

        // 创建新的 bitmap
        auto ctx = D2DDeviceManager::Instance().GetD2DContext();
        Microsoft::WRL::ComPtr<ID2D1Bitmap1> newBitmap;
        D2D1_BITMAP_PROPERTIES1 props = {};
        props.pixelFormat.format = DXGI_FORMAT_R8G8B8A8_UNORM;
        props.pixelFormat.alphaMode = D2D1_ALPHA_MODE_PREMULTIPLIED;
        ctx->CreateBitmap(D2D1_SIZE_U{ static_cast<UINT32>(newWidth), static_cast<UINT32>(newHeight) }, nullptr, 0, &props, &newBitmap);

        // 使用缩放绘制原 bitmap 到新 bitmap
        ctx->SetTarget(newBitmap.Get());
        ctx->BeginDraw();

        D2D1_SIZE_F size = m_bitmap->GetSize();
        D2D1_RECT_F destRect = D2D1::RectF(0, 0, static_cast<float>(newWidth), static_cast<float>(newHeight));
        D2D1_RECT_F srcRect = D2D1::RectF(0, 0, size.width, size.height);

        ctx->DrawBitmap(m_bitmap.Get(), destRect, 1.0f, D2D1_BITMAP_INTERPOLATION_MODE_LINEAR, srcRect);
        ctx->EndDraw();

        m_bitmap = newBitmap;
        m_width = newWidth;
        m_height = newHeight;
    }

    void Layer::RotateLayer(double angleDegrees)
    {
        m_rotation += angleDegrees;

        // 将角度归一化到 [0, 360)
        if (m_rotation >= 360.0) m_rotation -= 360.0;
        if (m_rotation < 0.0) m_rotation += 360.0;

        // 渲染时在 RenderToWin2D 或 GetContext 绘制前应用旋转矩阵
        // ctx->SetTransform(D2D1::Matrix3x2F::Rotation(m_rotation, D2D1::Point2F(m_width/2.0f, m_height/2.0f)));
    }

    void Layer::FlipHorizontalLayer()
    {
        m_flipH = !m_flipH;
        // 渲染时应用翻转矩阵：
        // ctx->SetTransform(D2D1::Matrix3x2F::Scale(m_flipH?-1:1, m_flipV? -1:1, D2D1::Point2F(m_width/2.0f, m_height/2.0f)));
    }

    void Layer::FlipVerticalLayer()
    {
        m_flipV = !m_flipV;
        // 渲染时同上
    }

}
