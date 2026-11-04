using System;
using Microsoft.Extensions.DependencyInjection;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using Workloads.Entry.Interfaces;
using Workloads.Utils.DraftUtils.Interfaces;

namespace Workloads.Entry {
    public class RuntimeFactory : IRuntimeFactory {
        public RuntimeFactory() : this(CreateStaticRuntime, CreateWebRuntime) { }

        internal RuntimeFactory(Func<IRuntime> staticRuntimeActivator, Func<IRuntime> webRuntimeActivator) {
            _staticRuntimeActivator = staticRuntimeActivator ?? throw new ArgumentNullException(nameof(staticRuntimeActivator));
            _webRuntimeActivator = webRuntimeActivator ?? throw new ArgumentNullException(nameof(webRuntimeActivator));
        }

        public IRuntime Create(string file, FileType type) {
            IRuntime runtime = type switch {
                FileType.FDesign or FileType.FImage => _staticRuntimeActivator(),
                FileType.FWebDesign => _webRuntimeActivator(),
                _ => throw new ArgumentOutOfRangeException(nameof(type), $"Unsupported file type: {type}"),
            };

            runtime.Initialize(file, type);
            return runtime;
        }

        private static IRuntime CreateStaticRuntime() =>
            ActivatorUtilities.CreateInstance<Creation.StaticImg.MainPage>(AppServiceLocator.Services);

        private static IRuntime CreateWebRuntime() =>
            ActivatorUtilities.CreateInstance<Creation.WebBackdrop.MainPage>(AppServiceLocator.Services);

        private readonly Func<IRuntime> _staticRuntimeActivator;
        private readonly Func<IRuntime> _webRuntimeActivator;
    }
}
