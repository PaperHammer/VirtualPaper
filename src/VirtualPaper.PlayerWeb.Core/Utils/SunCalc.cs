using System;

namespace VirtualPaper.PlayerWeb.Core.Utils {
    public static class SunCalc {
        /// <summary>
        /// 计算指定日期、经纬度的日出日落时间（UTC）
        /// </summary>
        public static (DateTime sunrise, DateTime sunset) Calculate(DateTime date, double latitude, double longitude) {
            int dayOfYear = date.DayOfYear;
            double longitudeHour = longitude / 15.0;

            // 日出
            double tRise = dayOfYear + (6.0 - longitudeHour) / 24.0;
            double sunriseMeanAnomaly = (0.9856 * tRise) - 3.289;
            double sunriseUtc = ComputeUtcTime(tRise, sunriseMeanAnomaly, latitude, longitude, isSunrise: true);

            // 日落
            double tSet = dayOfYear + (18.0 - longitudeHour) / 24.0;
            double sunsetMeanAnomaly = (0.9856 * tSet) - 3.289;
            double sunsetUtc = ComputeUtcTime(tSet, sunsetMeanAnomaly, latitude, longitude, isSunrise: false);

            var baseDate = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);
            var sunrise = baseDate.AddHours(sunriseUtc);
            var sunset = baseDate.AddHours(sunsetUtc);

            return (sunrise, sunset);
        }

        private static double ComputeUtcTime(double t, double meanAnomaly, double lat, double lon, bool isSunrise) {
            double lngHour = lon / 15.0;
            double M = meanAnomaly;
            double L = M + (1.916 * Math.Sin(DegToRad(M)))
                         + (0.020 * Math.Sin(DegToRad(2 * M)))
                         + 282.634;
            L = NormalizeDeg(L);

            double RA = Math.Atan(0.91764 * Math.Tan(DegToRad(L)));
            RA = RadToDeg(RA);
            RA = NormalizeDeg(RA);

            double lQuadrant = Math.Floor(L / 90) * 90;
            double raQuadrant = Math.Floor(RA / 90) * 90;
            RA = (RA + lQuadrant - raQuadrant) / 15.0;

            double sinDec = 0.39782 * Math.Sin(DegToRad(L));
            double cosDec = Math.Cos(Math.Asin(sinDec));

            double zenith = 90.833; // 标准太阳圆面 + 大气折射
            double cosH = (Math.Cos(DegToRad(zenith)) - sinDec * Math.Sin(DegToRad(lat)))
                          / (cosDec * Math.Cos(DegToRad(lat)));

            // cosH 超出 [-1,1] 说明该地区当天无日出/日落（极昼极夜）
            cosH = Math.Clamp(cosH, -1.0, 1.0);

            double H = isSunrise
                ? RadToDeg(Math.Acos(cosH))
                : 360.0 - RadToDeg(Math.Acos(cosH));

            H /= 15.0;

            double T = H + RA - (0.06571 * t) - 6.622;
            double utc = T - lngHour;
            return ((utc % 24) + 24) % 24; // 归一化到 [0, 24)
        }

        private static double DegToRad(double d) => d * Math.PI / 180.0;
        private static double RadToDeg(double r) => r * 180.0 / Math.PI;
        private static double NormalizeDeg(double d) => ((d % 360) + 360) % 360;
    }
}
