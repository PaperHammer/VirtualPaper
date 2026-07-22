using VirtualPaper.Common;

namespace VirtualPaper.DraftPanel.Model {
    public enum ProjectOpenIntent {
        OpenExisting,
        CreateFromName,
        CreateAtDirectory,
    }

    /// <summary>
    /// 预加载项目数据
    /// </summary>
    /// <param name="Identity">项目的标识：可能是绝对文件路径 (e.g. "C:\Work\A.vpd")，也可能是新建项目的名称 (e.g. "MyNewProject")</param>
    /// <param name="Type">项目类型</param>
    /// <param name="Intent">项目打开意图</param>
    public record PreProjectData(string Identity, ProjectType Type, ProjectOpenIntent Intent);
}
