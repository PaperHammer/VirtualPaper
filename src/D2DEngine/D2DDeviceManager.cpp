#include "pch.h"
#include "D2DDeviceManager.h"
#include <stdexcept>
using Microsoft::WRL::ComPtr;

D2DDeviceManager& D2DDeviceManager::Instance()
{
    static D2DDeviceManager instance;
    return instance;
}

D2DDeviceManager::D2DDeviceManager()
{
    Initialize();
}

void D2DDeviceManager::Initialize()
{
    UINT creationFlags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
#if defined(_DEBUG)
    creationFlags |= D3D11_CREATE_DEVICE_DEBUG;
#endif

    D3D_FEATURE_LEVEL featureLevels[] = {
        D3D_FEATURE_LEVEL_11_1,
        D3D_FEATURE_LEVEL_11_0
    };
    D3D_FEATURE_LEVEL featureLevel;

    HRESULT hr = D3D11CreateDevice(
        nullptr,
        D3D_DRIVER_TYPE_HARDWARE,
        nullptr,
        creationFlags,
        featureLevels,
        ARRAYSIZE(featureLevels),
        D3D11_SDK_VERSION,
        &m_d3dDevice,
        &featureLevel,
        &m_d3dContext);

    if (FAILED(hr)) {
        throw std::runtime_error("Failed to create D3D11 device");
    }

    // 获取 DXGI 设备
    m_d3dDevice.As(&m_dxgiDevice);

    // 创建 D2D 工厂
    D2D1_FACTORY_OPTIONS options = {};
#if defined(_DEBUG)
    options.debugLevel = D2D1_DEBUG_LEVEL_INFORMATION;
#endif

    hr = D2D1CreateFactory(
        D2D1_FACTORY_TYPE_SINGLE_THREADED,
        __uuidof(ID2D1Factory7),
        &options,
        reinterpret_cast<void**>(m_d2dFactory.ReleaseAndGetAddressOf()));

    if (FAILED(hr)) {
        throw std::runtime_error("Failed to create D2D factory");
    }

    // 创建设备和上下文
    hr = m_d2dFactory->CreateDevice(m_dxgiDevice.Get(), &m_d2dDevice);
    if (FAILED(hr)) {
        throw std::runtime_error("Failed to create D2D device");
    }

    hr = m_d2dDevice->CreateDeviceContext(
        D2D1_DEVICE_CONTEXT_OPTIONS_NONE,
        &m_d2dContext);

    if (FAILED(hr)) {
        throw std::runtime_error("Failed to create D2D device context");
    }
}

ComPtr<ID3D11Device> D2DDeviceManager::GetD3DDevice() const
{
    return m_d3dDevice;
}

ComPtr<ID3D11DeviceContext> D2DDeviceManager::GetD3DContext() const
{
    return m_d3dContext;
}

ComPtr<ID2D1DeviceContext5> D2DDeviceManager::GetD2DContext() const
{
    return m_d2dContext;
}

ComPtr<ID2D1Factory7> D2DDeviceManager::GetD2DFactory() const
{
    return m_d2dFactory;
}

ComPtr<ID2D1Device5> D2DDeviceManager::GetD2DDevice() const
{
    return m_d2dDevice;
}

ComPtr<ID2D1Bitmap1> D2DDeviceManager::CreateRenderTargetForLayer(int32_t width, int32_t height)
{
    D2D1_BITMAP_PROPERTIES1 props = D2D1::BitmapProperties1(
        D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS_CANNOT_DRAW,
        D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED)
    );

    ComPtr<ID2D1Bitmap1> bitmap;
    HRESULT hr = m_d2dContext->CreateBitmap(
        D2D1::SizeU(width, height),
        nullptr,
        0,
        &props,
        &bitmap);

    if (FAILED(hr)) {
        throw std::runtime_error("Failed to create render target bitmap");
    }

    return bitmap;
}