#pragma once
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <d2d1_3.h>
#include <vector>

class PointUtils
{
public:
    // 单点转换：D2D -> WinRT
    static winrt::Windows::Foundation::Point ToWinrtPoint(const D2D1_POINT_2F& point)
    {
        return { point.x, point.y };
    }

    // 单点转换：WinRT -> D2D
    static D2D1_POINT_2F ToD2DPoint(const winrt::Windows::Foundation::Point& point)
    {
        return { point.X, point.Y };
    }

    // 集合转换：vector<D2D> -> IVector<Point> 
    static winrt::Windows::Foundation::Collections::IVector<winrt::Windows::Foundation::Point>
        ToWinrtPoints(const std::vector<D2D1_POINT_2F>& points)
    {
        auto result = winrt::single_threaded_vector<winrt::Windows::Foundation::Point>();
        for (const auto& p : points) {
            result.Append(ToWinrtPoint(p));
        }
        return result;
    }

    // 集合转换：IVector<Point> -> vector<D2D>
    static std::vector<D2D1_POINT_2F>
        ToD2DPoints(const winrt::Windows::Foundation::Collections::IVector<winrt::Windows::Foundation::Point>& points)
    {
        std::vector<D2D1_POINT_2F> result;
        result.reserve(points.Size());
        for (const auto& p : points) {
            result.push_back(ToD2DPoint(p));
        }
        return result;
    }

    // 批量转换：D2D数组 -> IVector<Point>
    template<size_t N>
    static winrt::Windows::Foundation::Collections::IVector<winrt::Windows::Foundation::Point>
        ToWinrtPoints(const D2D1_POINT_2F(&points)[N])
    {
        auto result = winrt::single_threaded_vector<winrt::Windows::Foundation::Point>();
        for (size_t i = 0; i < N; ++i) {
            result.Append(ToWinrtPoint(points[i]));
        }
        return result;
    }
};