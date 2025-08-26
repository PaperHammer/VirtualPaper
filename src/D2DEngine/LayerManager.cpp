#include "pch.h"
#include "Layer.h"
#include "LayerManager.h"
#include "LayerManager.g.cpp"
#include <d3d11.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>
#include <windows.graphics.directx.direct3d11.interop.h>

namespace winrt::D2DEngine::implementation
{
	struct IBufferByteAccess : ::IUnknown
	{
		virtual HRESULT __stdcall Buffer(uint8_t** value) = 0;
	};

	LayerManager::LayerManager() {
		m_compositeContext = D2DDeviceManager::Instance().CreateIndependentD2DContext();
	}

	void LayerManager::AddLayer(winrt::D2DEngine::Layer const& layer)
	{
		m_layers.push_back(layer);
	}

	void LayerManager::RemoveLayer(int32_t index)
	{
		if (index >= 0 && index < static_cast<int32_t>(m_layers.size()))
			m_layers.erase(m_layers.begin() + index);
	}

	void LayerManager::MoveLayer(int32_t fromIndex, int32_t toIndex)
	{
		if (fromIndex >= 0 && fromIndex < static_cast<int32_t>(m_layers.size()) &&
			toIndex >= 0 && toIndex < static_cast<int32_t>(m_layers.size()) &&
			fromIndex != toIndex)
		{
			auto& layer = m_layers[fromIndex];
			m_layers.erase(m_layers.begin() + fromIndex);
			m_layers.insert(m_layers.begin() + toIndex, layer);
		}
	}

	winrt::D2DEngine::Layer LayerManager::GetLayer(int32_t index)
	{
		if (index >= 0 && index < static_cast<int32_t>(m_layers.size()))
			return m_layers[index];
		throw hresult_out_of_bounds();
	}

	int32_t LayerManager::LayerCount() const
	{
		return static_cast<int32_t>(m_layers.size());
	}

	void LayerManager::Clear()
	{
		m_layers.clear();
	}

	void LayerManager::Render()
	{
		if (m_isDirty)
		{
			RenderAll();
			m_isDirty = false;
		}
	}

	void LayerManager::EnsureCompositeTarget()
	{
		if (m_compositeBitmap && !m_isDirty) return;

		auto newBitmap = D2DDeviceManager::Instance().CreateRenderTargetForLayer(m_width, m_height, m_compositeContext);
		if (!newBitmap) {
			throw winrt::hresult_error(E_FAIL, L"Failed to create composite render target");
		}

		m_compositeBitmap = newBitmap;
		m_isDirty = false;
	}

	void LayerManager::RenderAll()
	{
		EnsureCompositeTarget();

		auto ctx = m_compositeContext.get();
		ctx->SetTarget(m_compositeBitmap.get());
		ctx->BeginDraw();
		ctx->Clear(D2D1::ColorF(0, 0, 0, 0)); // 透明背景

		for (auto const& layer : m_layers)
		{
			auto impl = winrt::get_self<implementation::Layer>(layer);
			if (impl->Visible())
			{
				auto bmp = impl->GetBitmap();
				if (bmp)
				{
					D2D1_RECT_F rect = D2D1::RectF(0, 0, (float)impl->Width(), (float)impl->Height());
					ctx->DrawBitmap(
						bmp.get(),
						rect,
						impl->Opacity(),
						D2D1_BITMAP_INTERPOLATION_MODE_LINEAR
					);
				}
			}
		}

		HRESULT hr = ctx->EndDraw();
		if (FAILED(hr)) {
			m_isDirty = true; // 标记失败，下次重试
			throw winrt::hresult_error(hr, L"Composite draw failed");
		}
	}

	winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DSurface LayerManager::GetSurface()
	{
		RenderAll();

		if (!m_compositeBitmap) {
			return nullptr;
		}

		// 获取DXGI表面
		winrt::com_ptr<IDXGISurface> dxgiSurface;
		winrt::check_hresult(m_compositeBitmap->GetSurface(dxgiSurface.put()));

		// 转换为Direct3DSurface
		winrt::com_ptr<::IInspectable> surfaceInspectable;
		winrt::check_hresult(
			CreateDirect3D11SurfaceFromDXGISurface(
				dxgiSurface.get(),
				reinterpret_cast<::IInspectable**>(surfaceInspectable.put()))
		);

		return surfaceInspectable.as<winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DSurface>();
	}

	winrt::Windows::Foundation::IAsyncOperation<winrt::Windows::Storage::Streams::IBuffer> LayerManager::RenderToBufferAsync()
	{
		uint32_t width = m_width;
		uint32_t height = m_height;
		if (!m_compositeBitmap) co_return nullptr;

		uint32_t stride = width * 4; // 32位BGRA格式
		D2D1_MAPPED_RECT mapped = {};
		HRESULT hr = m_compositeBitmap->Map(D2D1_MAP_OPTIONS_READ, &mapped);
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

		m_compositeBitmap->Unmap();
		co_return buffer;
	}
}