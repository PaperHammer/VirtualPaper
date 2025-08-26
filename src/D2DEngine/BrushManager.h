#pragma once
#include <d2d1_3.h>
#include <mutex>

namespace winrt::D2DEngine::implementation
{
	struct BrushManager : implements<BrushManager, IBrushManager>
	{
	public:
		static winrt::D2DEngine::IBrushManager GetInstance();

		float StrokeWidth() const;
		void StrokeWidth(float value);

		StrokeTool StrokeType() const;
		void StrokeType(StrokeTool value);

		void SetSolidColor(winrt::Windows::UI::Color color);
		void SetGradient(
			winrt::Windows::Foundation::Point startPoint,
			winrt::Windows::Foundation::Point endPoint,
			winrt::Windows::Foundation::Collections::IVector<winrt::Windows::UI::Color> const& gradientStops);
		void CreateImageBrush(winrt::Windows::Storage::Streams::IBuffer const& imageData);

		winrt::com_ptr<ID2D1Brush> CurrentBrush();

	private:
		BrushManager();
		// 允许 make_self 访问私有构造函数
		friend struct winrt::impl::heap_implements<BrushManager>;
		static winrt::com_ptr<BrushManager> s_instance;
		// 防拷贝
		BrushManager(const BrushManager&) = delete;
		BrushManager& operator=(const BrushManager&) = delete;

		mutable std::mutex m_mutex;
		winrt::com_ptr<ID2D1SolidColorBrush> m_solidBrush;
		winrt::com_ptr<ID2D1LinearGradientBrush> m_gradientBrush;
		winrt::com_ptr<ID2D1ImageBrush> m_imageBrush;
		float m_strokeWidth{ 1.0f };
		StrokeTool m_strokeType{ StrokeTool::Brush };

		winrt::com_ptr<ID2D1DeviceContext5> m_ctx;

		void EnsureDeviceResources();
		void ReleaseDeviceResources();
	};
}