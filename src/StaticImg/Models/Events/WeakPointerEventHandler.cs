using System;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Workloads.Creation.StaticImg.Views.Components;

namespace Workloads.Creation.StaticImg.Models.Events
{
    class WeakPointerEventHandler
    {
        public WeakPointerEventHandler(LayerManager manager) {
            _weakManager = new WeakReference<LayerManager>(manager);
        }

        public void OnDraw(CanvasControl sender, CanvasDrawEventArgs args) {
            if (_weakManager.TryGetTarget(out LayerManager manager)) {
                manager.OnDraw(sender, args);
            }
        }

        private readonly WeakReference<LayerManager> _weakManager;
    }
}
