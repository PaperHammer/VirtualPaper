#pragma once
#include <d3d11.h>
#include <d2d1_3.h>
#include <mutex>

class D2DDeviceManager
{
public:
    static D2DDeviceManager& Instance();

    // 获取设备接口
    winrt::com_ptr<ID3D11Device> GetD3DDevice() const;
    winrt::com_ptr<ID2D1Factory7> GetD2DFactory() const;
    winrt::com_ptr<ID2D1Device5> GetD2DDevice() const;

    // 创建独立上下文
    winrt::com_ptr<ID2D1DeviceContext5> CreateIndependentD2DContext() const;
    winrt::com_ptr<ID3D11DeviceContext> CreateIndependentD3DContext() const;
	// 获取共享设备上下文
	winrt::com_ptr<ID2D1DeviceContext5> GetSharedD2DContext() const;
	winrt::com_ptr<ID3D11DeviceContext> GetSharedD3DContext() const;

    // 创建渲染目标
    winrt::com_ptr<ID2D1Bitmap1> CreateRenderTargetForLayer(int32_t width, int32_t height, winrt::com_ptr<ID2D1DeviceContext5> d2dContext);

private:
    D2DDeviceManager();
    // 防拷贝
    D2DDeviceManager(const D2DDeviceManager&) = delete;
    D2DDeviceManager& operator=(const D2DDeviceManager&) = delete;

    void InitializeDirect3D();
    void InitializeDirect2D();

    mutable std::mutex m_mutex;
    winrt::com_ptr<ID3D11Device> m_d3dSharedDevice;
    winrt::com_ptr<IDXGIDevice> m_dxgiSharedDevice;
    winrt::com_ptr<ID2D1Factory7> m_d2dFactory;
    winrt::com_ptr<ID2D1Device5> m_d2dSharedDevice;
    winrt::com_ptr<ID2D1DeviceContext5> m_d2dSharedContext;
    winrt::com_ptr<ID3D11DeviceContext> m_d3dSharedContext;
};