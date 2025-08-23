#include "pch.h"
#include "Layer.h"
#include "LayerManager.h"
#include "LayerManager.g.cpp"

namespace winrt::D2DEngine::implementation
{
	LayerManager::~LayerManager() noexcept
	{
		if (m_sharedHandle)
		{
			CloseHandle(m_sharedHandle);
			m_sharedHandle = nullptr;
		}
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

	HRESULT LayerManager::Resize(uint32_t width, uint32_t height)
	{
		if (width == 0 || height == 0) return E_INVALIDARG;
		if (width == m_width && height == m_height) return S_OK;

		m_width = width;
		m_height = height;

		// 释放现有资源
		m_renderTarget = nullptr;
		m_sharedTexture = nullptr;

		// 创建共享纹理
		D3D11_TEXTURE2D_DESC desc = {};
		desc.Width = width;
		desc.Height = height;
		desc.MipLevels = 1;
		desc.ArraySize = 1;
		desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
		desc.SampleDesc.Count = 1;
		desc.Usage = D3D11_USAGE_DEFAULT;
		desc.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
		desc.MiscFlags = D3D11_RESOURCE_MISC_SHARED;

		HRESULT hr = m_deviceManager.GetD3DDevice()->CreateTexture2D(
			&desc, nullptr, &m_sharedTexture);
		if (FAILED(hr)) return hr;

		// 获取共享句柄
		ComPtr<IDXGIResource> dxgiResource;
		hr = m_sharedTexture.As(&dxgiResource);
		if (FAILED(hr)) return hr;

		hr = dxgiResource->GetSharedHandle(&m_sharedHandle);
		if (FAILED(hr)) return hr;

		// 创建D2D渲染目标
		ComPtr<IDXGISurface> surface;
		hr = m_sharedTexture.As(&surface);
		if (FAILED(hr)) return hr;

		auto props = D2D1::BitmapProperties1(
			D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS_CANNOT_DRAW,
			D2D1::PixelFormat(desc.Format, D2D1_ALPHA_MODE_PREMULTIPLIED)
		);

		return m_deviceManager.GetD2DContext()->CreateBitmapFromDxgiSurface(
			surface.Get(), &props, &m_renderTarget);
	}

	HRESULT LayerManager::RenderToSharedTexture()
	{
		if (!m_renderTarget || !m_deviceManager.GetD2DContext())
			return E_NOT_VALID_STATE;

		auto context = m_deviceManager.GetD2DContext();

		// 设置渲染目标
		context->SetTarget(m_renderTarget.Get());
		context->BeginDraw();
		context->Clear(D2D1::ColorF(0, 0, 0, 0)); // 透明背景

		// 渲染所有可见图层
		for (auto const& layer : m_layers)
		{
			auto impl = winrt::get_self<implementation::Layer>(layer);
			if (impl->Visible())
			{
				auto bmp = impl->GetBitmap();
				if (bmp)
				{
					context->DrawBitmap(
						bmp.Get(),
						D2D1::RectF(0, 0, impl->Width(), impl->Height()),
						impl->Opacity(),
						D2D1_BITMAP_INTERPOLATION_MODE_LINEAR
					);
				}
			}
		}

		return context->EndDraw();
	}

	HANDLE LayerManager::GetSharedHandle() const
	{
		return m_sharedHandle;
	}

	void LayerManager::RenderAll()
	{
		auto context = m_deviceManager.GetD2DContext();
		if (!context) return;

		context->BeginDraw();
		context->Clear(D2D1::ColorF(0, 0, 0, 0)); // 透明背景

		for (auto const& layer : m_layers)
		{
			auto impl = winrt::get_self<implementation::Layer>(layer);
			if (impl->Visible())
			{
				auto bmp = impl->GetBitmap();
				if (bmp)
				{
					context->DrawBitmap(
						bmp.Get(),
						D2D1::RectF(0, 0, impl->Width(), impl->Height()),
						impl->Opacity(),
						D2D1_BITMAP_INTERPOLATION_MODE_LINEAR
					);
				}
			}
		}

		context->EndDraw();
	}
}