#pragma once
#include <vector>
#include "D2DEngine.LayerManager.g.h"
#include "D2DDeviceManager.h"

namespace winrt::D2DEngine::implementation
{
    struct LayerManager : LayerManagerT<LayerManager>
    {
        ~LayerManager() noexcept;
        LayerManager() = default;

        // 图层管理
        void AddLayer(D2DEngine::Layer const& layer);
        void RemoveLayer(int32_t index);
        void MoveLayer(int32_t fromIndex, int32_t toIndex);
        D2DEngine::Layer GetLayer(int32_t index);
        int32_t LayerCount() const;
        void Clear();

        // 渲染接口
        void RenderAll();

        // 共享纹理管理
        HRESULT Resize(uint32_t width, uint32_t height);
        HRESULT RenderToSharedTexture();
        HANDLE GetSharedHandle() const;

    private:
        // 使用设备管理器中的资源
        D2DDeviceManager& m_deviceManager = D2DDeviceManager::Instance();

        // 共享纹理资源
        ComPtr<ID3D11Texture2D> m_sharedTexture;
        ComPtr<ID2D1Bitmap1> m_renderTarget;
        HANDLE m_sharedHandle = nullptr;

        // 图层数据
        std::vector<D2DEngine::Layer> m_layers;
        uint32_t m_width = 0;
        uint32_t m_height = 0;
    };
}

namespace winrt::D2DEngine::factory_implementation
{
    struct LayerManager : LayerManagerT<LayerManager, implementation::LayerManager> {};
}