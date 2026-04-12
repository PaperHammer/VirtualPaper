namespace VirtualPaper.Common.Utils.DI {
    public static class AppServiceLocator {
        private static IServiceProvider _services = null!;
        public static IServiceProvider Services {
            get {
                if (_services == null) {
                    throw new InvalidOperationException("AppServiceLocator has not been initialized. Please set the Current property");
                }
                return _services;
            }
            set => _services = value;
        }
    }
}
