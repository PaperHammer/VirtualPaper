using System;

namespace Workloads.Creation.StaticImg.Utils.History.Models {
    public sealed partial class Layerage : IDisposable {
        public string Id { get; set; } = string.Empty;
        public Layerage[]? Children { get; set; }

        public override string ToString() => this.Id;

        public void Dispose() {
            this.Id = string.Empty;
            if (this.Children is null) return;

            foreach (Layerage item in this.Children) {
                item.Dispose();
            }

            Array.Clear(this.Children, 0, this.Children.Length);
            this.Children = null;
        }

    }
}
