namespace Workloads.Creation.StaticImg.Models.ToolItems.Utils.Base {
    /*
     * 用于记录每个需要适配 undo/redo 的操作
     * 
     * 轨迹绘制：受影响的每个点 oldColor 与 newColor
     * 区域选择：受影响的两个区域 posOrigin posDest 的像素信息 origin-originOldRect 与 dest-destOldRect
     */
    internal abstract record StructuredRecord {
    }
}
