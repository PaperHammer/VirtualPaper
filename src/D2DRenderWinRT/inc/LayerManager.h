#pragma once
#include <vector>
#include "D2DEngine.LayerManager.g.h"
using namespace Microsoft::WRL;
using namespace winrt::Microsoft::Graphics::Canvas;

namespace winrt::D2DEngine::implementation
{
    struct LayerManager : LayerManagerT<LayerManager>
    {
		~LayerManager() noexcept = default;

        LayerManager();

        void AddLayer(D2DEngine::Layer const& layer);
        void RemoveLayer(int32_t index);
        void MoveLayer(int32_t fromIndex, int32_t toIndex);

        D2DEngine::Layer GetLayer(int32_t index);
        int32_t LayerCount();

        void Clear();

        // 新增: 渲染所有可见图层到指定上下文
        void RenderAll(ComPtr<ID2D1DeviceContext5> const& targetContext);
        void RenderToWin2D(Canvas::CanvasRenderTarget const& target);
        void RenderToCanvasControl(winrt::Microsoft::Graphics::Canvas::UI::Xaml::CanvasControl const& canvas);
        void RenderToCanvasControl(winrt::Microsoft::Graphics::Canvas::UI::Xaml::CanvasControl const& canvas);

    private:
        std::vector<D2DEngine::Layer> m_layers;
    };
}

namespace winrt::D2DEngine::factory_implementation
{
    struct LayerManager : LayerManagerT<LayerManager, implementation::LayerManager> {};
}
