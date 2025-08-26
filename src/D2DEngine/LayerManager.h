#pragma once
#include "LayerManager.g.h"
#include <vector>
#include "D2DDeviceManager.h"
#include <d2d1_1.h>

namespace winrt::D2DEngine::implementation
{
	struct LayerManager : LayerManagerT<LayerManager>
	{
		LayerManager();

		int32_t LayerCount() const;

		// 图层管理
		winrt::D2DEngine::Layer GetLayer(int32_t index);
		void AddLayer(winrt::D2DEngine::Layer const& layer);
		void RemoveLayer(int32_t index);
		void MoveLayer(int32_t fromIndex, int32_t toIndex);
		void Clear();

		// 渲染
		void Render(); // 触发合成（可被调用方控制时机）
		// 高性能：返回 GPU 表面
		winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DSurface GetSurface();
		// 低频使用：返回 CPU 图像（用于截图、导出）
		winrt::Windows::Foundation::IAsyncOperation<winrt::Windows::Storage::Streams::IBuffer> RenderToBufferAsync();

	private:
		// 图层数据
		std::vector<winrt::D2DEngine::Layer> m_layers;
		uint32_t m_width = 0;
		uint32_t m_height = 0;

		// 渲染
		winrt::com_ptr<ID2D1Bitmap1> m_compositeBitmap; // 最终合成图像
		winrt::com_ptr<ID2D1DeviceContext5> m_compositeContext; // 专用合成上下文        
		bool m_isDirty = true; // 标记是否需要重新合成

		void EnsureCompositeTarget();
		void RenderAll();
	};
}

namespace winrt::D2DEngine::factory_implementation
{
	struct LayerManager : LayerManagerT<LayerManager, implementation::LayerManager> {};
}