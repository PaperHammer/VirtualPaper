namespace VirtualPaper.Common.Utils.Bridge.Base {
    public interface IOjectProvider {
        T GetRequiredService<T>(
            ObjectLifetime lifetime = ObjectLifetime.Transient,
            ObjectLifetime lifetimeForParams = ObjectLifetime.Transient,
            object? scope = null);
    }
}
