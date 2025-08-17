#pragma once
#include <d2d1_3.h>
#include <d3d11_4.h>
#include <wrl/client.h>
#include <cstdint>
using namespace Microsoft::WRL;

class D2DDeviceManager
{
public:
    static D2DDeviceManager& Instance();

    // 获取设备接口
    ComPtr<ID3D11Device> GetD3DDevice() const;
    ComPtr<ID3D11DeviceContext> GetD3DContext() const;
    ComPtr<ID2D1DeviceContext5> GetD2DContext() const;
    ComPtr<ID2D1Factory7> GetD2DFactory() const;
    ComPtr<ID2D1Device5> GetD2DDevice() const;

    // 创建渲染目标
    ComPtr<ID2D1Bitmap1> CreateRenderTargetForLayer(int32_t width, int32_t height);

private:
    D2DDeviceManager();
    void Initialize();

    ComPtr<ID3D11Device> m_d3dDevice;
    ComPtr<ID3D11DeviceContext> m_d3dContext;
    ComPtr<IDXGIDevice> m_dxgiDevice;
    ComPtr<ID2D1Factory7> m_d2dFactory;
    ComPtr<ID2D1Device5> m_d2dDevice;
    ComPtr<ID2D1DeviceContext5> m_d2dContext;
};