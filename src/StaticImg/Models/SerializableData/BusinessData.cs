using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Windows.UI;

namespace Workloads.Creation.StaticImg.Models.SerializableData {
    public class BusinessData {
        public const int MAX_COLORS = 10;

        public int SelectedLayerIndex => _selectedLayerIndex;
        public IReadOnlyList<Color> Colors => _colors.AsReadOnly();

        public void SetSelectedLayerIndex(int selectedLayerIndex) {
            _selectedLayerIndex = selectedLayerIndex; 
        }

        public void SetColors(IEnumerable<Color> colors) {
            _colors.Clear();

            ArgumentNullException.ThrowIfNull(colors, nameof(colors));
            foreach (var color in colors) {
                if (_colors.Count >= MAX_COLORS)
                    break;

                _colors.Add(color);
            }
        }

        public static byte[] Serialize(BusinessData data) {
            using var ms = new MemoryStream(CalculateSerializedSize(data));
            using var writer = new BinaryWriter(ms);

            // 状态
            writer.Write(data.SelectedLayerIndex);

            // 颜色
            writer.Write((ushort)data._colors.Count);
            foreach (ref readonly var color in CollectionsMarshal.AsSpan(data._colors)) {
                writer.Write(color.R);
                writer.Write(color.G);
                writer.Write(color.B);
                writer.Write(color.A);
            }

            return ms.ToArray();
        }

        public static BusinessData Deserialize(byte[] data) {
            if (data == null || data.Length < sizeof(int) + sizeof(ushort))
                throw new ArgumentException("Invalid data");

            var instance = new BusinessData();
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            int selectedLayerIndex = reader.ReadInt32();
            instance.SetSelectedLayerIndex(selectedLayerIndex);

            ushort count = reader.ReadUInt16();
            instance._colors.Capacity = count;            
            for (int i = 0; i < count; i++) {
                instance._colors.Add(new Color {
                    R = reader.ReadByte(),
                    G = reader.ReadByte(),
                    B = reader.ReadByte(),
                    A = reader.ReadByte()
                });
            }

            return instance;
        }

        private static int CalculateSerializedSize(BusinessData data) =>
            sizeof(int) // SelectedLayerIndex
            + sizeof(ushort) // Color count
            + (data._colors.Count * Marshal.SizeOf<Color>());

        internal BusinessData Clone() {
            var data = new BusinessData();
            data.SetColors(this.Colors);
            data.SetSelectedLayerIndex(this.SelectedLayerIndex);
            return data;
        }

        private readonly List<Color> _colors = new(MAX_COLORS);
        private int _selectedLayerIndex;
    }
}
