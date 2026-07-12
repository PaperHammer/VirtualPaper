using System;
using Microsoft.Extensions.DependencyInjection;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using Workloads.Entry.Interfaces;
using Workloads.Utils.DraftUtils.Interfaces;

namespace Workloads.Utils {
    public class RuntimeFactory : IRuntimeFactory {
        public IRuntime Create(string file, FileType type) => type switch {
            FileType.FDesign => AppServiceLocator.Services.GetRequiredService<Creation.StaticImg.MainPage>(),
            FileType.FWebDesign => AppServiceLocator.Services.GetRequiredService<Creation.WebBackdrop.MainPage>(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), $"Unsupported file type: {type}"),
        };
    }
}
