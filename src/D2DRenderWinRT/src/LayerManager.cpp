#include "pch.h"
#include "LayerManager.h"
#include <../winrt/Microsoft.Graphics.Canvas.h>
#include <../winrt/Microsoft.Graphics.Canvas.UI.Xaml.h>

namespace winrt::D2DEngine::implementation
{
	LayerManager::LayerManager() {}

	void LayerManager::AddLayer(D2DEngine::Layer const& layer)
	{
		m_layers.push_back(layer);
	}

	void LayerManager::RemoveLayer(int32_t index)
	{
		if (index >= 0 && index < (int)m_layers.size())
			m_layers.erase(m_layers.begin() + index);
	}

	void LayerManager::MoveLayer(int32_t fromIndex, int32_t toIndex)
	{
		if (fromIndex >= 0 && fromIndex < (int)m_layers.size() &&
			toIndex >= 0 && toIndex < (int)m_layers.size() && fromIndex != toIndex)
		{
			auto layer = m_layers[fromIndex];
			m_layers.erase(m_layers.begin() + fromIndex);
			m_layers.insert(m_layers.begin() + toIndex, layer);
		}
	}

	D2DEngine::Layer LayerManager::GetLayer(int32_t index)
	{
		if (index >= 0 && index < (int)m_layers.size())
			return m_layers[index];
		throw hresult_out_of_bounds();
	}

	int32_t LayerManager::LayerCount()
	{
		return (int)m_layers.size();
	}

	void LayerManager::Clear()
	{
		m_layers.clear();
	}

	void LayerManager::RenderAll(Microsoft::WRL::ComPtr<ID2D1DeviceContext5> const& targetContext)
	{
		if (!targetContext) return;

		// 开始批处理绘制
		targetContext->BeginDraw();

		for (auto const& layer : m_layers)
		{
			auto impl = winrt::get_self<winrt::D2DEngine::implementation::Layer>(layer);
			if (impl->Visible())
			{
				auto bmp = impl->GetBitmap();
				if (bmp)
				{
					D2D1_RECT_F destRect = D2D1::RectF(0, 0, (FLOAT)impl->Width(), (FLOAT)impl->Height());
					targetContext->DrawBitmap(
						bmp.Get(),
						&destRect,
						(FLOAT)impl->Opacity(),
						D2D1_BITMAP_INTERPOLATION_MODE_LINEAR
					);
				}
			}
		}

		targetContext->EndDraw();
	}

	void LayerManager::RenderToWin2D(winrt::Microsoft::Graphics::Canvas::CanvasRenderTarget const& target)
	{
		if (!target) return;

		// 获取 DXGI Surface
		auto surface = target.as<Windows::Graphics::DirectX::Direct3D11::IDirect3DSurface>();
		winrt::com_ptr<IDirect3DSurface> nativeSurface = surface.as<IDirect3DSurface>();

		Microsoft::WRL::ComPtr<IDXGISurface> dxgiSurface;
		winrt::check_hresult(nativeSurface->GetNativeResource(
			reinterpret_cast<void**>(dxgiSurface.put()),
			sizeof(IDXGISurface)
		));

		// 创建临时 D2D 渲染目标
		auto d2dCtx = D2DDeviceManager::Instance().GetD2DContext();
		Microsoft::WRL::ComPtr<ID2D1Bitmap1> targetBitmap;
		winrt::check_hresult(
			d2dCtx->CreateBitmapFromDxgiSurface(
				dxgiSurface.Get(),
				nullptr,
				&targetBitmap
			)
		);

		d2dCtx->SetTarget(targetBitmap.Get());

		// 开始绘制
		d2dCtx->BeginDraw();

		for (auto const& layer : m_layers)
		{
			auto impl = winrt::get_self<winrt::D2DEngine::implementation::Layer>(layer);
			if (impl->Visible())
			{
				auto bmp = impl->GetBitmap();
				if (bmp)
				{
					D2D1_RECT_F destRect = D2D1::RectF(
						0, 0,
						(FLOAT)impl->Width(),
						(FLOAT)impl->Height()
					);
					d2dCtx->DrawBitmap(
						bmp.Get(),
						&destRect,
						(FLOAT)impl->Opacity(),
						D2D1_BITMAP_INTERPOLATION_MODE_LINEAR
					);
				}
			}
		}

		winrt::check_hresult(d2dCtx->EndDraw());
	}

	void LayerManager::RenderToCanvasControl(
		winrt::Microsoft::Graphics::Canvas::UI::Xaml::CanvasControl const& canvas)
	{
		if (!canvas) return;

		// 注册 Draw 事件
		canvas.Draw([](auto const& sender, winrt::Microsoft::Graphics::Canvas::UI::Xaml::CanvasDrawEventArgs const& args)
			{
				auto ds = args.DrawingSession();

				// 获取底层 D2D context
				auto device = ds.Device();
				auto d2dCtx = device.as<ID2D1DeviceContext5>();

				if (!d2dCtx) return;

				// 清空背景
				ds.Clear(Windows::UI::Colors::Transparent);

				// 遍历所有 Layer
				for (auto const& layer : m_layers)
				{
					auto impl = winrt::get_self<winrt::D2DEngine::implementation::Layer>(layer);
					if (impl->Visible())
					{
						auto bmp = impl->GetBitmap();
						if (bmp)
						{
							// 绘制到 ds
							D2D1_RECT_F destRect = D2D1::RectF(
								0, 0,
								(FLOAT)impl->Width(),
								(FLOAT)impl->Height()
							);

							d2dCtx->DrawBitmap(
								bmp.Get(),
								&destRect,
								(FLOAT)impl->Opacity(),
								D2D1_BITMAP_INTERPOLATION_MODE_LINEAR
							);
						}
					}
				}
			});
	}

	void LayerManager::RenderToCanvasControl(
		winrt::Microsoft::Graphics::Canvas::UI::Xaml::CanvasControl const& canvas)
	{
		canvas.Draw([this](auto const& sender, auto const& args)
			{
				auto ds = args.DrawingSession();
				auto device = ds.Device();
				auto d2dCtx = device.as<ID2D1DeviceContext5>();

				if (!d2dCtx) return;

				ds.Clear(Windows::UI::Colors::Transparent);

				for (auto const& layer : m_layers)
				{
					auto impl = winrt::get_self<winrt::D2DEngine::implementation::Layer>(layer);
					if (impl->Visible())
					{
						auto bmp = impl->GetBitmap();
						if (bmp)
						{
							D2D1_RECT_F destRect = D2D1::RectF(
								0, 0,
								(FLOAT)impl->Width(),
								(FLOAT)impl->Height()
							);
							d2dCtx->DrawBitmap(bmp.Get(), &destRect, (FLOAT)impl->Opacity(),
								D2D1_BITMAP_INTERPOLATION_MODE_LINEAR);
						}
					}
				}
			});
	}

}
