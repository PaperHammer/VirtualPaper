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

    ComPtr<ID3D11Device> device;
    ComPtr<ID3D11DeviceContext> context;

    D3D_FEATURE_LEVEL featureLevels[] = { D3D_FEATURE_LEVEL_11_1, D3D_FEATURE_LEVEL_11_0 };
    D3D_FEATURE_LEVEL featureLevel;

    if (FAILED(D3D11CreateDevice(
        nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, creationFlags,
        featureLevels, _countof(featureLevels),
        D3D11_SDK_VERSION, &device, &featureLevel, &context)))
    {
        throw std::runtime_error("Failed to create D3D11 device");
    }

    m_d3dDevice = device;
    m_d3dContext = context;

    // 获取 DXGI 设备
    device.As(&m_dxgiDevice);

    // 创建 D2D 工厂
    D2D1_FACTORY_OPTIONS options = {};
#if defined(_DEBUG)
    options.debugLevel = D2D1_DEBUG_LEVEL_INFORMATION;
#endif
    D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, IID_PPV_ARGS(&m_d2dFactory));

    // 创建设备和上下文
    m_d2dFactory->CreateDevice(m_dxgiDevice.Get(), &m_d2dDevice);
    m_d2dDevice->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE, &m_d2dContext);
}

ComPtr<ID2D1DeviceContext5> D2DDeviceManager::GetD2DContext()
{
    return m_d2dContext;
}

ComPtr<ID2D1Factory7> D2DDeviceManager::GetD2DFactory()
{
    return m_d2dFactory;
}

ComPtr<ID2D1Bitmap1> D2DDeviceManager::CreateRenderTargetForLayer(int32_t width, int32_t height)
{
    D2D1_BITMAP_PROPERTIES1 props = D2D1::BitmapProperties1(
        D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS_CANNOT_DRAW,
        D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED)
    );

    Microsoft::WRL::ComPtr<ID2D1Bitmap1> bitmap;
    D2DDeviceManager::Instance().GetD2DContext()->CreateBitmap(
        D2D1::SizeU(width, height), nullptr, 0, &props, &bitmap);
    return bitmap;
}
