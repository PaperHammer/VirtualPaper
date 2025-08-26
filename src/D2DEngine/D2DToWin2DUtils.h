#pragma once
#include <winrt/Microsoft.Graphics.Canvas.h>
#include <d2d1_3.h>
#include <Windows.Graphics.DirectX.Direct3D11.interop.h>
#include <d3d11.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>

class D2DToWin2DUtils
{
public:
    // 将ID2D1Bitmap1转换为CanvasBitmap
    static winrt::Microsoft::Graphics::Canvas::CanvasBitmap
        ConvertToCanvasBitmap(
            winrt::com_ptr<ID2D1Bitmap1> d2dBitmap,
            winrt::Microsoft::Graphics::Canvas::CanvasDevice const& canvasDevice)
    {
        if (!d2dBitmap)
            throw winrt::hresult_invalid_argument(L"d2dBitmap cannot be null");

        // 获取DXGI表面
        winrt::com_ptr<IDXGISurface> dxgiSurface;
        HRESULT hr = d2dBitmap->GetSurface(dxgiSurface.put());
        if (FAILED(hr))
            throw winrt::hresult_error(hr, L"Failed to get DXGI surface");

        // 转换为WinRT接口
        auto dxgiSurfaceRt = CreateDirect3DSurface(dxgiSurface.get());

        // 创建CanvasBitmap
        return winrt::Microsoft::Graphics::Canvas::CanvasBitmap::CreateFromDirect3D11Surface(
            canvasDevice,
            dxgiSurfaceRt);
    }

private:
    // 创建IDirect3DSurface的辅助函数
    static winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DSurface
        CreateDirect3DSurface(IDXGISurface* dxgiSurface)
    {
        winrt::com_ptr<::IInspectable> surfaceInspectable;
        HRESULT hr = CreateDirect3D11SurfaceFromDXGISurface(
            dxgiSurface,
            reinterpret_cast<::IInspectable**>(surfaceInspectable.put()));

        if (FAILED(hr))
            throw winrt::hresult_error(hr, L"Failed to create Direct3DSurface");

        return surfaceInspectable.as<winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DSurface>();
    }
};