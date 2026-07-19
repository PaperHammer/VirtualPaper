using System;
using Microsoft.Extensions.DependencyInjection;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using Workloads.Entry.Interfaces;
using Workloads.Utils.DraftUtils.Interfaces;

namespace Workloads.Entry {
    public class RuntimeFactory : IRuntimeFactory {
        public IRuntime Create(string file, FileType type) {
            IRuntime runtime = type switch {
                FileType.FDesign or FileType.FImage => ActivatorUtilities.CreateInstance<Creation.StaticImg.MainPage>(AppServiceLocator.Services),
                FileType.FWebDesign => ActivatorUtilities.CreateInstance<Creation.WebBackdrop.MainPage>(AppServiceLocator.Services),
                _ => throw new ArgumentOutOfRangeException(nameof(type), $"Unsupported file type: {type}"),
            };

            runtime.Initialize(file, type);
            return runtime;
        }
    }
}
