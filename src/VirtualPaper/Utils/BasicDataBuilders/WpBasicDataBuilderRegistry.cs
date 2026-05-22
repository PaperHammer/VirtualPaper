using VirtualPaper.Common;

namespace VirtualPaper.Utils.BasicDataBuilders {
    /// <summary>
    /// FileType → IWpBasicDataBuilder 的注册与查找。
    /// 添加新类型只需调用 Register，无需修改其他代码。
    /// </summary>
    internal class WpBasicDataBuilderRegistry {
        private readonly Dictionary<FileType, IWpBasicDataBuilder> _builders = [];

        public WpBasicDataBuilderRegistry Register(FileType fileType, IWpBasicDataBuilder builder) {
            _builders[fileType] = builder;
            return this;
        }

        /// <summary>
        /// 获取对应 FileType 的 Builder，找不到时抛出明确异常。
        /// </summary>
        public IWpBasicDataBuilder Get(FileType fileType) {
            if (_builders.TryGetValue(fileType, out var builder))
                return builder;

            throw new NotSupportedException($"FileType '{fileType}' 尚未注册对应的 Builder。");
        }
    }
}
