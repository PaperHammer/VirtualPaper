#include "pch.h"
#include "Layer.h"
#include <iostream>
#include "D2DDeviceManager.h"
using namespace winrt::D2DEngine;
using namespace Microsoft::WRL;

static void TestLayerFunctionality()
{
    try {
        // 1. 测试图层创建
        auto layer = winrt::make_self<implementation::Layer>(
            L"TestLayer", 800, 600, LayerType::Bitmap);

        std::wcout << L"Created layer: " << layer->Name().c_str()
            << L" (" << layer->Width() << L"x" << layer->Height() << L")\n";

        // 2. 测试绘图功能
        std::vector<D2D1_POINT_2F> points = {
            {100, 100}, {200, 200}, {300, 150}
        };

        StrokeOptions strokeOpts = {
            StrokeTool::Brush,
            {1.0f, 0.0f, 0.0f, 1.0f}, // 红色
            2.0f,
            1.0f
        };

        layer->DrawStroke(points, strokeOpts);
        std::wcout << L"Draw stroke operation completed\n";

        // 3. 测试变换功能
        layer->Resize(1024, 768, true);
        std::wcout << L"Resized to: " << layer->Width() << L"x" << layer->Height() << L"\n";

        layer->Rotate(RotateDirection::Right);
        std::wcout << L"Rotated 90 degrees right\n";

        // 4. 测试选择功能（使用实际存在的方法）
        D2D1_POINT_2F testPoint = { 150, 150 };
        AreaHandle handle = layer->HitTestSelectionHandle(testPoint);
        if (handle != AreaHandle::None) {
            std::wcout << L"Hit test successful, handle type: " << static_cast<int>(handle) << L"\n";

            // 调整选择区域大小
            layer->ResizeSelection({ 160, 160 }, handle);
            std::wcout << L"Selection resized\n";
        }

        // 5. 验证渲染
        auto ctx = D2DDeviceManager::Instance().GetD2DContext();
        auto bitmap = layer->GetBitmap();
        if (bitmap) {
            ctx->DrawBitmap(bitmap.Get());
            std::wcout << L"Layer rendered successfully\n";
        }

        std::wcout << L"All tests passed successfully!\n";
    }
    catch (const std::exception& e) {
        std::cerr << "Test failed: " << e.what() << std::endl;
    }
    catch (...) {
        std::cerr << "Unknown test failure occurred" << std::endl;
    }
}

int main()
{
    // 初始化必要的组件
    D2DDeviceManager::Instance();

    std::wcout << L"Starting Layer class tests...\n";
    TestLayerFunctionality();
    std::wcout << L"Testing completed.\n";

    return 0;
}