#pragma once
#include <d2d1_3.h>
#include <d3d11_4.h>
#include <wrl/client.h>
#include <cstdint>

class D2DDeviceManager
{
public:
    static D2DDeviceManager& Instance();

    // 获取设备接口
    Microsoft::WRL::ComPtr<ID3D11Device> GetD3DDevice() const;
    Microsoft::WRL::ComPtr<ID3D11DeviceContext> GetD3DContext() const;
    Microsoft::WRL::ComPtr<ID2D1DeviceContext5> GetD2DContext() const;
    Microsoft::WRL::ComPtr<ID2D1Factory7> GetD2DFactory() const;
    Microsoft::WRL::ComPtr<ID2D1Device5> GetD2DDevice() const;

    // 创建渲染目标
    Microsoft::WRL::ComPtr<ID2D1Bitmap1> CreateRenderTargetForLayer(int32_t width, int32_t height);

private:
    D2DDeviceManager();
    void Initialize();

    Microsoft::WRL::ComPtr<ID3D11Device> m_d3dDevice;
    Microsoft::WRL::ComPtr<ID3D11DeviceContext> m_d3dContext;
    Microsoft::WRL::ComPtr<IDXGIDevice> m_dxgiDevice;
    Microsoft::WRL::ComPtr<ID2D1Factory7> m_d2dFactory;
    Microsoft::WRL::ComPtr<ID2D1Device5> m_d2dDevice;
    Microsoft::WRL::ComPtr<ID2D1DeviceContext5> m_d2dContext;
};