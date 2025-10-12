using System;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Common;
using VirtualPaper.Models.Mvvm;

namespace Workloads.Creation.StaticImg.Models {
    public partial class LayerInfo : ObservableObject {
        public InkRenderData RenderData { get; set; }
        public Guid Tag => _tag;

        private string _name = string.Empty;
        public string Name {
            get => _name;
            set { if (_name == value) return; _name = value; OnPropertyChanged(); }
        }

        private bool _isVisible = true;
        public bool IsVisible {
            get => _isVisible;
            set {
                if (_isVisible == value) return;
                _isVisible = value;
                if (value) MainPage.Instance.Bridge.GetNotify().CloseAndRemoveMsg(nameof(Constants.I18n.Draft_SI_LayerLocked));
                OnPropertyChanged();
            }
        }

        public bool IsDeleted { get; set; }

        ImageSource _layerThum;
        public ImageSource LayerThum {
            get { return _layerThum; }
            set { _layerThum = value; OnPropertyChanged(); }
        }

        private readonly Guid _tag = Guid.NewGuid();
    }
}
