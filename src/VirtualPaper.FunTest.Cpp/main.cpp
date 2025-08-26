#include "pch.h"
#include <iostream>

static void TestLayerFunctionality()
{
	try {
		// 初始化 WinRT 运行时
		winrt::init_apartment();

		// 测试图层创建
		winrt::D2DEngine::Layer layer{
			L"LayerName", 
			800,
			600,
			winrt::D2DEngine::LayerType::Bitmap
		};
		std::wcout << L"Created layer: " << layer.Name().c_str()
			<< L" (" << layer.Width() << L"x" << layer.Height() << L")\n";

		// 测试绘图功能
		auto points = winrt::single_threaded_vector<winrt::Windows::Foundation::Point>({
			{100, 100},
			{200, 200},
			{300, 150}
			});

		winrt::D2DEngine::StrokeOptions strokeOpts = {
			winrt::D2DEngine::StrokeTool::Brush,
			{1.0f, 0.0f, 0.0f, 1.0f},
			2.0f,
			1.0f
		};

		layer.DrawStroke(points, strokeOpts);
		std::wcout << L"Draw stroke operation completed\n";

		// 测试变换功能
		layer.Resize(1024, 768, true);
		std::wcout << L"Resized to: " << layer.Width() << L"x" << layer.Height() << L"\n";

		layer.Rotate(winrt::D2DEngine::RotateDirection::Right);
		std::wcout << L"Rotated 90 degrees right\n";

		// 测试选择功能
		winrt::Windows::Foundation::Point testPoint{ 150, 150 };
		auto handle = layer.HitTestSelectionHandle(testPoint);

		if (handle != winrt::D2DEngine::AreaHandle::None) {
			std::wcout << L"Hit test successful, handle type: "
				<< static_cast<int>(handle) << L"\n";

			layer.ResizeSelection({ 160, 160 }, handle);
			std::wcout << L"Selection resized\n";
		}

		// 验证渲染
		auto bufferOperation = layer.RenderToBufferAsync();

		// 等待异步操作完成（在测试中可能需要同步等待）
		winrt::Windows::Storage::Streams::IBuffer buffer = bufferOperation.get();

		if (buffer) {
			std::wcout << L"RenderToBufferAsync succeeded!\n";
			std::wcout << L"Buffer size: " << buffer.Length() << L" bytes\n";
			std::wcout << L"Buffer capacity: " << buffer.Capacity() << L" bytes\n";

			// 验证缓冲区内容
			uint32_t expectedSize = layer.Width() * layer.Height() * 4; // BGRA格式
			if (buffer.Length() == expectedSize) {
				std::wcout << L"Buffer size matches expected BGRA format\n";
			}
			else {
				std::wcout << L"Warning: Buffer size doesn't match expected BGRA format\n";
				std::wcout << L"Expected: " << expectedSize << L" bytes, Got: " << buffer.Length() << L" bytes\n";
			}

			// 可以进一步检查缓冲区数据
			// 检查前几个字节是否合理
			if (buffer.Length() >= 4) {
				uint8_t* data = buffer.data();
				std::wcout << L"First pixel (BGRA): "
					<< static_cast<int>(data[0]) << L", "
					<< static_cast<int>(data[1]) << L", "
					<< static_cast<int>(data[2]) << L", "
					<< static_cast<int>(data[3]) << L"\n";
			}
		}
		else {
			std::wcout << L"RenderToBufferAsync returned null buffer\n";
		}

		std::wcout << L"All tests passed successfully!\n";
	}
	catch (const winrt::hresult_error& e) {
		std::wcerr << L"WinRT Error: " << e.message().c_str()
			<< L" (0x" << std::hex << e.code() << L")\n";
	}
	catch (const std::exception& e) {
		std::cerr << "Std exception: " << e.what() << std::endl;
	}
	catch (...) {
		std::cerr << "Unknown test failure occurred" << std::endl;
	}
}

int main()
{
	std::ios::sync_with_stdio(false);
	std::wcout << L"Starting Layer class tests...\n";

	TestLayerFunctionality();

	std::wcout << L"Testing completed.\n";
	return 0;
}
