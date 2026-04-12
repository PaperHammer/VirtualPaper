using System;

namespace VirtualPaper.UIComponent.Services {
    public interface IUnifiedInputProcessor<in TEventArgs> : ICursorService, IDisposable where TEventArgs : EventArgs {
        void HandleEntered(TEventArgs e);
        void HandlePressed(TEventArgs e);
        void HandleMoved(TEventArgs e);
        void HandleReleased(TEventArgs e);
        void HandleExited(TEventArgs e);

        //// 状态查询
        //bool IsInteracting { get; }
        //Point CurrentPosition { get; }

        // 配置
        float InteractionThreshold { get; set; } // 移动阈值（防抖）
    }
}
