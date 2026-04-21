using VirtualPaper.Common;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Models.RepoPanel.Interfaces {
    /// <summary>
    /// 桌宠核心数据接口
    /// </summary>
    public interface IDpBasicData : IBasicAssetData<IDpBasicData> {
        string EntryFile { get; set; }
        float DefaultScale { get; set; }

        /// <summary>
        /// 交互动作列表映射
        /// </summary>
        Dictionary<string, string> Actions { get; set; }

        DeskPetEngineType Type { get; set; }
    }
}
