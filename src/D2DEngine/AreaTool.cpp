#include "pch.h"
#include "AreaTool.h"

namespace winrt::D2DEngine::implementation
{
    // 构造函数已内联在头文件中

    void AreaTool::Start(D2D1_POINT_2F startPoint)
    {
        m_area.Rect = Windows::Foundation::Rect(startPoint.x, startPoint.y, 0, 0);
        m_area.OriginalRect = m_area.Rect;
        m_area.Active = true;
        m_area.HandleSize = 8.0f; // 默认控制点大小
        m_area.IsOverlay = m_isOverlay;
        m_area.OverlayOpacity = m_overlayOpacity;
    }

    void AreaTool::Update(D2D1_POINT_2F currentPoint)
    {
        if (!m_area.Active) return;

        float newWidth = currentPoint.x - m_area.Rect.X;
        float newHeight = currentPoint.y - m_area.Rect.Y;

        m_area.Rect = Windows::Foundation::Rect(
            m_area.Rect.X,
            m_area.Rect.Y,
            newWidth,
            newHeight
        );
    }

    void AreaTool::Render(ID2D1DeviceContext5* ctx, ID2D1Bitmap1* targetBitmap)
    {
        if (!m_area.Active || !ctx || !targetBitmap) return;

        ctx->SetTarget(targetBitmap);
        ctx->BeginDraw();

        // 绘制蒙层（如果是裁剪工具）
        if (m_area.IsOverlay)
        {
            winrt::com_ptr<ID2D1SolidColorBrush> overlayBrush;
            ctx->CreateSolidColorBrush(
                D2D1::ColorF(D2D1::ColorF::Black, m_area.OverlayOpacity),
                overlayBrush.put()
            );

            D2D1_RECT_F rect = ConvertToD2DRect();
            ctx->PushAxisAlignedClip(rect, D2D1_ANTIALIAS_MODE_PER_PRIMITIVE);
            ctx->FillRectangle(D2D1::RectF(0, 0, 9999, 9999), overlayBrush.get());
            ctx->PopAxisAlignedClip();
        }

        // 绘制虚线框
        winrt::com_ptr<ID2D1StrokeStyle> strokeStyle;
        D2D1_STROKE_STYLE_PROPERTIES props = {};
        props.dashStyle = D2D1_DASH_STYLE_DASH;
        GetFactory()->CreateStrokeStyle(&props, nullptr, 0, strokeStyle.put());

        winrt::com_ptr<ID2D1SolidColorBrush> borderBrush;
        ctx->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::White), borderBrush.put());

        D2D1_RECT_F rect = ConvertToD2DRect();
        ctx->DrawRectangle(rect, borderBrush.get(), 1.0f, strokeStyle.get());

        // 绘制控制点
        auto handles = GetControlPoints();
        for (auto& pt : handles)
        {
            D2D1_RECT_F handleRect = {
                pt.x - m_area.HandleSize,
                pt.y - m_area.HandleSize,
                pt.x + m_area.HandleSize,
                pt.y + m_area.HandleSize
            };
            ctx->FillRectangle(handleRect, borderBrush.get());
        }

        ctx->EndDraw();
    }

    AreaHandle AreaTool::HitTestHandle(D2D1_POINT_2F pt) const
    {
        if (!m_area.Active) return AreaHandle::None;

        auto Hit = [&](float cx, float cy) {
            return pt.x >= cx - m_area.HandleSize && pt.x <= cx + m_area.HandleSize &&
                pt.y >= cy - m_area.HandleSize && pt.y <= cy + m_area.HandleSize;
            };

        D2D1_RECT_F rect = ConvertToD2DRect();
        float centerX = rect.left + (rect.right - rect.left) / 2.0f;
        float centerY = rect.top + (rect.bottom - rect.top) / 2.0f;

        if (Hit(rect.left, rect.top)) return AreaHandle::TopLeft;
        if (Hit(centerX, rect.top)) return AreaHandle::TopCenter;
        if (Hit(rect.right, rect.top)) return AreaHandle::TopRight;
        if (Hit(rect.left, centerY)) return AreaHandle::MiddleLeft;
        if (Hit(rect.right, centerY)) return AreaHandle::MiddleRight;
        if (Hit(rect.left, rect.bottom)) return AreaHandle::BottomLeft;
        if (Hit(centerX, rect.bottom)) return AreaHandle::BottomCenter;
        if (Hit(rect.right, rect.bottom)) return AreaHandle::BottomRight;

        if (pt.x > rect.left && pt.x < rect.right &&
            pt.y > rect.top && pt.y < rect.bottom)
            return AreaHandle::Move;

        return AreaHandle::None;
    }

    void AreaTool::Resize(D2D1_POINT_2F newPt, AreaHandle handle)
    {
        if (!m_area.Active || handle == AreaHandle::None) return;

        D2D1_RECT_F rect = ConvertToD2DRect();

        switch (handle)
        {
        case AreaHandle::TopLeft:      rect.left = newPt.x; rect.top = newPt.y; break;
        case AreaHandle::TopCenter:    rect.top = newPt.y; break;
        case AreaHandle::TopRight:     rect.right = newPt.x; rect.top = newPt.y; break;
        case AreaHandle::MiddleLeft:   rect.left = newPt.x; break;
        case AreaHandle::MiddleRight:  rect.right = newPt.x; break;
        case AreaHandle::BottomLeft:   rect.left = newPt.x; rect.bottom = newPt.y; break;
        case AreaHandle::BottomCenter: rect.bottom = newPt.y; break;
        case AreaHandle::BottomRight:  rect.right = newPt.x; rect.bottom = newPt.y; break;
        case AreaHandle::Move:
        {
            float dx = newPt.x - (rect.left + rect.right) / 2.0f;
            float dy = newPt.y - (rect.top + rect.bottom) / 2.0f;
            rect.left += dx; rect.right += dx;
            rect.top += dy; rect.bottom += dy;
        } break;
        default: break;
        }

        if (rect.left > rect.right) std::swap(rect.left, rect.right);
        if (rect.top > rect.bottom) std::swap(rect.top, rect.bottom);

        m_area.Rect = Windows::Foundation::Rect(
            rect.left,
            rect.top,
            rect.right - rect.left,
            rect.bottom - rect.top
        );
    }

    void AreaTool::Confirm()
    {
        m_area.Active = false;
    }

    void AreaTool::Cancel()
    {
        m_area = Area();
    }

    std::vector<D2D1_POINT_2F> AreaTool::GetControlPoints() const
    {
        D2D1_RECT_F rect = ConvertToD2DRect();
        float centerX = rect.left + (rect.right - rect.left) / 2.0f;
        float centerY = rect.top + (rect.bottom - rect.top) / 2.0f;

        return {
            {rect.left, rect.top},
            {centerX, rect.top},
            {rect.right, rect.top},
            {rect.left, centerY},
            {rect.right, centerY},
            {rect.left, rect.bottom},
            {centerX, rect.bottom},
            {rect.right, rect.bottom}
        };
    }

    D2D1_RECT_F AreaTool::ConvertToD2DRect() const
    {
        return D2D1::RectF(
            m_area.Rect.X,
            m_area.Rect.Y,
            m_area.Rect.X + m_area.Rect.Width,
            m_area.Rect.Y + m_area.Rect.Height
        );
    }
}