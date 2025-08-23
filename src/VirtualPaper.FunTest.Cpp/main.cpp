#include "pch.h"

using namespace winrt;
using namespace Windows::Foundation;

int main()
{
    init_apartment();
    Uri uri(L"http://aka.ms/cppwinrt");
    printf("Hello, %ls!\n", uri.AbsoluteUri().c_str());
}
//
//#include "pch.h"
//#include <iostream>
//#include <winrt/D2DRender.h>
//
//static void TestLayerFunctionality()
//{
//    try {
//        // 1. 测试图层创建
//        auto layer = winrt::make_self<winrt::D2DRender::implementation::Layer>(
//            L"TestLayer", 800, 600, winrt::D2DRender::LayerType::Bitmap);
//
//        std::wcout << L"Created layer: " << layer->Name().c_str()
//            << L" (" << layer->Width() << L"x" << layer->Height() << L")\n";
//
//        // 2. 测试绘图功能
//        vector<D2D1_POINT_2F> points = {
//            {100, 100}, {200, 200}, {300, 150}
//        };
//
//        winrt::D2DRender::StrokeOptions strokeOpts = {
//            winrt::D2DRender::StrokeTool::Brush,
//            {1.0f, 0.0f, 0.0f, 1.0f}, // 红色
//            2.0f,
//            1.0f
//        };
//
//        layer->DrawStroke(points, strokeOpts);
//        std::wcout << L"Draw stroke operation completed\n";
//
//        // 3. 测试变换功能
//        layer->Resize(1024, 768, true);
//        std::wcout << L"Resized to: " << layer->Width() << L"x" << layer->Height() << L"\n";
//
//        layer->Rotate(winrt::D2DRender::RotateDirection::Right);
//        std::wcout << L"Rotated 90 degrees right\n";
//
//        // 4. 测试选择功能（使用实际存在的方法）
//        D2D1_POINT_2F testPoint = { 150, 150 };
//        winrt::D2DRender::AreaHandle handle = layer->HitTestSelectionHandle(testPoint);
//        if (handle != winrt::D2DRender::AreaHandle::None) {
//            std::wcout << L"Hit test successful, handle type: " << static_cast<int>(handle) << L"\n";
//
//            // 调整选择区域大小
//            layer->ResizeSelection({ 160, 160 }, handle);
//            std::wcout << L"Selection resized\n";
//        }
//
//        // 5. 验证渲染
//        auto ctx = D2DDeviceManager::Instance().GetD2DContext();
//        auto bitmap = layer->GetBitmap();
//        if (bitmap) {
//            ctx->DrawBitmap(bitmap.Get());
//            std::wcout << L"Layer rendered successfully\n";
//        }
//
//        std::wcout << L"All tests passed successfully!\n";
//    }
//    catch (const std::exception& e) {
//        std::cerr << "Test failed: " << e.what() << std::endl;
//    }
//    catch (...) {
//        std::cerr << "Unknown test failure occurred" << std::endl;
//    }
//}
//
//int main()
//{
//    ios::sync_with_stdio(false);
//
//    // 初始化必要的组件
//    D2DDeviceManager::Instance();
//
//    std::wcout << L"Starting Layer class tests...\n";
//    TestLayerFunctionality();
//    std::wcout << L"Testing completed.\n";
//
//    return 0;
//}
