#pragma once
#include "Layer.h"
#include "D2DDeviceManager.h"
#include <d2d1_3.h>
#include <wrl/client.h>
using Microsoft::WRL::ComPtr;

namespace winrt::D2DEngine::implementation
{
    class AreaTool
    {
    public:
        AreaTool(bool isOverlay = false, float overlayOpacity = 1.0f)
            : m_isOverlay(isOverlay), m_overlayOpacity(overlayOpacity) {
        }

        // 区域操作
        void Start(D2D1_POINT_2F startPoint);
        void Update(D2D1_POINT_2F currentPoint);
        void Confirm();
        void Cancel();

        // 渲染
        void Render(ID2D1DeviceContext5* ctx, ID2D1Bitmap1* targetBitmap);

        // 交互
        AreaHandle HitTestHandle(D2D1_POINT_2F pt) const;
        void Resize(D2D1_POINT_2F newPt, AreaHandle handle);

        // 获取当前状态
        Area GetState() const { return m_area; }
        void SetState(const Area& area) { m_area = area; }

    private:
        Area m_area;
        bool m_isOverlay;
        float m_overlayOpacity;
        ComPtr<ID2D1Bitmap1> m_areaBitmap;

        // 使用 D2DDeviceManager 获取资源
        ComPtr<ID2D1Factory7> GetFactory() const {
            return D2DDeviceManager::Instance().GetD2DFactory();
        }

        ComPtr<ID2D1DeviceContext5> GetContext() const {
            return D2DDeviceManager::Instance().GetD2DContext();
        }

        std::vector<D2D1_POINT_2F> GetControlPoints() const;
        D2D1_RECT_F ConvertToD2DRect() const;
    };
}