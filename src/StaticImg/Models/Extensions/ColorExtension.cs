//using System;
//using Windows.UI;

//namespace Workloads.Creation.StaticImg.Models.Extensions {
//    public static class ColorExtension {
//        public static byte[] ToByteArgb(this Color color) {
//            return [color.A, color.R, color.G, color.B];
//        }

//        public static Color ToColor(this byte[] bytes) {
//            if (bytes.Length != 4) {
//                throw new ArgumentException("Byte array must have exactly 4 elements representing ARGB.");
//            }
//            return Color.FromArgb(bytes[0], bytes[1], bytes[2], bytes[3]);
//        }
//    }
//}
