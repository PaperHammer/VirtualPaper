#pragma once
#include <d2d1_3.h>
#include <d3d11_4.h>
#include <dxgi1_6.h>
#include <wrl/client.h>

class D2DDeviceManager
{
public:
	static D2DDeviceManager& Instance();

	ComPtr<ID2D1DeviceContext5> GetD2DContext();
	ComPtr<ID2D1Factory7> GetD2DFactory();

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

