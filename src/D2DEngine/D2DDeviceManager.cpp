#include "pch.h"
#include "D2DDeviceManager.h"
#include <stdexcept>

D2DDeviceManager& D2DDeviceManager::Instance()
{
    static D2DDeviceManager instance;
    return instance;
}

D2DDeviceManager::D2DDeviceManager()
{
    InitializeDirect3D();
    InitializeDirect2D();
}

// 初始化D3D设备
void D2DDeviceManager::InitializeDirect3D()
{
    UINT creationFlags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
#if defined(_DEBUG)
    creationFlags |= D3D11_CREATE_DEVICE_DEBUG;
#endif

    D3D_FEATURE_LEVEL featureLevels[] = {
        D3D_FEATURE_LEVEL_11_1,
        D3D_FEATURE_LEVEL_11_0
    };

    winrt::check_hresult(
        D3D11CreateDevice(
            nullptr,
            D3D_DRIVER_TYPE_HARDWARE,
            nullptr,
            creationFlags,
            featureLevels,
            ARRAYSIZE(featureLevels),
            D3D11_SDK_VERSION,
            m_d3dSharedDevice.put(),
            nullptr,
            m_d3dSharedContext.put()
        )
    );
}

// 初始化D2D设备
void D2DDeviceManager::InitializeDirect2D()
{
    D2D1_FACTORY_OPTIONS options = {};
#if defined(_DEBUG)
    options.debugLevel = D2D1_DEBUG_LEVEL_INFORMATION;
#endif

    // 创建D2D工厂
    winrt::check_hresult(
        D2D1CreateFactory(
            D2D1_FACTORY_TYPE_SINGLE_THREADED,
            __uuidof(ID2D1Factory7),
            &options,
            m_d2dFactory.put_void()
        )
    );

    // 获取DXGI设备
    winrt::com_ptr<IDXGIDevice> dxgiDevice;
    m_d3dSharedDevice->QueryInterface(dxgiDevice.put());

    // 创建D2D设备
    winrt::check_hresult(
        m_d2dFactory->CreateDevice(
            dxgiDevice.get(),
            m_d2dSharedDevice.put()
        )
    );

    // 创建设备上下文
    winrt::check_hresult(
        m_d2dSharedDevice->CreateDeviceContext(
            D2D1_DEVICE_CONTEXT_OPTIONS_NONE,
            m_d2dSharedContext.put()
        )
    );
}

winrt::com_ptr<ID3D11Device> D2DDeviceManager::GetD3DDevice() const
{
    return m_d3dSharedDevice;
}

winrt::com_ptr<ID2D1Factory7> D2DDeviceManager::GetD2DFactory() const
{
    return m_d2dFactory;
}

winrt::com_ptr<ID2D1Device5> D2DDeviceManager::GetD2DDevice() const
{
    return m_d2dSharedDevice;
}

winrt::com_ptr<ID2D1DeviceContext5> D2DDeviceManager::GetSharedD2DContext() const
{
	return m_d2dSharedContext;
}

winrt::com_ptr<ID3D11DeviceContext> D2DDeviceManager::GetSharedD3DContext() const
{
	return m_d3dSharedContext;
}

winrt::com_ptr<ID2D1DeviceContext5> D2DDeviceManager::CreateIndependentD2DContext() const
{
    std::lock_guard lock(m_mutex);
    if (!m_d3dSharedDevice) {
        throw winrt::hresult_error(DXGI_ERROR_DEVICE_REMOVED, L"D2D device not available");
    }

    winrt::com_ptr<ID2D1DeviceContext5> context;
    HRESULT hr = m_d2dSharedDevice->CreateDeviceContext(
        D2D1_DEVICE_CONTEXT_OPTIONS_NONE,
        reinterpret_cast<ID2D1DeviceContext**>(context.put())
    );

    if (FAILED(hr)) {
        throw winrt::hresult_error(hr, L"Failed to create device context");
    }
    return context;
}

winrt::com_ptr<ID3D11DeviceContext> D2DDeviceManager::CreateIndependentD3DContext() const
{
    std::lock_guard lock(m_mutex);
    if (!m_d3dSharedDevice) {
        throw winrt::hresult_error(DXGI_ERROR_DEVICE_REMOVED, L"D3D device not available");
    }

    winrt::com_ptr<ID3D11DeviceContext> context;
    m_d3dSharedDevice->GetImmediateContext(context.put());
    return context;
}

winrt::com_ptr<ID2D1Bitmap1> D2DDeviceManager::CreateRenderTargetForLayer(int32_t width, int32_t height, winrt::com_ptr<ID2D1DeviceContext5> d2dContext)
{
    D2D1_BITMAP_PROPERTIES1 props = D2D1::BitmapProperties1(
        D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS_CANNOT_DRAW,
        D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED)
    );

    winrt::com_ptr<ID2D1Bitmap1> bitmap;
    HRESULT hr = d2dContext->CreateBitmap(
        D2D1::SizeU(width, height),
        nullptr,
        0,
        &props,
        bitmap.put());

    if (FAILED(hr)) {
        throw std::runtime_error("Failed to create render target bitmap");
    }

    return bitmap;
}