#include "pch.h"
#include "BrushManager.h"
#include "D2DDeviceManager.h"
#include <d2d1_3.h>
#include <d3d11_4.h>
#include <dxgi1_6.h>
#include <winrt/Windows.Storage.Streams.h>

namespace winrt::D2DEngine::implementation
{
    winrt::com_ptr<BrushManager> BrushManager::s_instance;

    winrt::D2DEngine::IBrushManager  BrushManager::GetInstance()
    {
        if (!s_instance)
        {
            s_instance = winrt::make_self<BrushManager>();
        }
        return s_instance.as<winrt::D2DEngine::IBrushManager>();
    }        

    BrushManager::BrushManager()
    {
        m_ctx = D2DDeviceManager::Instance().CreateIndependentD2DContext();
        EnsureDeviceResources();
    }

    float BrushManager::StrokeWidth() const { return m_strokeWidth; }
    void BrushManager::StrokeWidth(float value) { m_strokeWidth = value; }

    StrokeTool BrushManager::StrokeType() const { return m_strokeType; }
    void BrushManager::StrokeType(StrokeTool value) { m_strokeType = value; }

    void BrushManager::SetSolidColor(winrt::Windows::UI::Color color)
    {
        m_strokeType = StrokeTool::Brush;
        m_imageBrush = nullptr;
        m_gradientBrush = nullptr;

        if (!m_solidBrush) {
            EnsureDeviceResources();
        }
        m_solidBrush->SetColor(D2D1::ColorF(
            color.R / 255.0f,
            color.G / 255.0f,
            color.B / 255.0f,
            color.A / 255.0f
        ));
    }

    // 渐变画笔设置
    void BrushManager::SetGradient(
        winrt::Windows::Foundation::Point startPoint,
        winrt::Windows::Foundation::Point endPoint,
        winrt::Windows::Foundation::Collections::IVector<winrt::Windows::UI::Color> const& gradientStops)
    {
        m_strokeType = StrokeTool::Brush;
        m_solidBrush = nullptr;
        m_imageBrush = nullptr;

        // 转换渐变点
        std::vector<D2D1_GRADIENT_STOP> stops(gradientStops.Size());
        for (uint32_t i = 0; i < gradientStops.Size(); ++i) {
            auto color = gradientStops.GetAt(i);
            stops[i].color = D2D1::ColorF(
                color.R / 255.0f,
                color.G / 255.0f,
                color.B / 255.0f,
                color.A / 255.0f
            );
            stops[i].position = static_cast<float>(i) / (gradientStops.Size() - 1);
        }

        // 创建渐变集合
        winrt::com_ptr<ID2D1GradientStopCollection> stopCollection;
        winrt::check_hresult(
            m_ctx->CreateGradientStopCollection(
                stops.data(),
                static_cast<UINT32>(stops.size()),
                D2D1_GAMMA_2_2,
                D2D1_EXTEND_MODE_CLAMP,
                stopCollection.put()
            )
        );

        // 创建渐变画笔
        winrt::check_hresult(
            m_ctx->CreateLinearGradientBrush(
                D2D1::LinearGradientBrushProperties(
                    D2D1::Point2F(startPoint.X, startPoint.Y),
                    D2D1::Point2F(endPoint.X, endPoint.Y)
                ),
                stopCollection.get(),
                m_gradientBrush.put()
            )
        );
    }

    // 图像画笔创建
    void BrushManager::CreateImageBrush(Windows::Storage::Streams::IBuffer const& imageData)
    {
        m_strokeType = StrokeTool::Brush;
        m_solidBrush = nullptr;
        m_gradientBrush = nullptr;

        // 实现图像加载逻辑（需补充实际实现）
        throw winrt::hresult_not_implemented();
    }

    // 获取当前画笔（内部使用）
    winrt::com_ptr<ID2D1Brush> BrushManager::CurrentBrush()
    {
        if (m_imageBrush) return m_imageBrush;
        if (m_gradientBrush) return m_gradientBrush;
        return m_solidBrush; // 默认返回纯色画笔
    }

    // 确保设备资源
    void BrushManager::EnsureDeviceResources()
    {
        if (!m_solidBrush) {
            winrt::check_hresult(
                m_ctx->CreateSolidColorBrush(
                    D2D1::ColorF(D2D1::ColorF::White),
                    m_solidBrush.put()
                )
            );
        }
    }

    // 释放设备资源
    void BrushManager::ReleaseDeviceResources()
    {
        m_solidBrush = nullptr;
        m_gradientBrush = nullptr;
        m_imageBrush = nullptr;
    }
}