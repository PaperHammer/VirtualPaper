using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace VirtualPaper.Common.Utils.DI {
    public static class ObjectProvider {
        #region common
        public static T GetRequiredService<T>(ObjectLifetime lifetime = ObjectLifetime.Transient, ObjectLifetime lifetimeForParams = ObjectLifetime.Singleton, object? scope = null) {
            var type = typeof(T);

            // 如果是接口或抽象类，则查找注册的实现类型
            if (type.IsInterface || type.IsAbstract) {
                if (!_relations.TryGetValue(type, out var implementationType)) {
                    throw new InvalidOperationException($"No implementation found for service type {type.FullName}.");
                }
                type = implementationType;
            }

            switch (lifetime) {
                case ObjectLifetime.Singleton:
                    return (T)_singletonInstances.GetOrAdd(type, _ => CreateInstance<T>(type, lifetimeForParams, scope));
                case ObjectLifetime.Scoped:
                    if (scope == null)
                        throw new InvalidOperationException("Scope cannot be null for scoped services.");

                    var scopedDict = _scopedInstances.GetOrCreateValue(scope);
                    if (!scopedDict.TryGetValue(type, out object value)) {
                        value = CreateInstance<T>(type, lifetimeForParams, scope);
                        scopedDict[type] = value;
                    }
                    return (T)value;
                case ObjectLifetime.Transient:
                default:
                    return CreateInstance<T>(type, lifetimeForParams, scope);
            }
        }

        public static void RegisterRelation<TInterface, TImplementation>() where TImplementation : TInterface, new() {
            if (_relations.ContainsKey(typeof(TInterface))) return;

            var interfaceType = typeof(TInterface);
            var implementationType = typeof(TImplementation);

            _relations.TryAdd(interfaceType, implementationType);
        }

        public static void Clean() {
            // 清理所有单例实例
            foreach (var instance in _singletonInstances.Values) {
                if (instance is IDisposable disposable) {
                    disposable.Dispose();
                }
            }
            _singletonInstances.Clear();

            // 清理所有作用域实例
            foreach (var scopedDict in _scopedInstances) {
                foreach (var instance in scopedDict.Value.Values) {
                    if (instance is IDisposable disposable) {
                        disposable.Dispose();
                    }
                }
            }
            _scopedInstances.Clear();
        }

        private static T CreateInstance<T>(Type type, ObjectLifetime lifetimeForParams, object scope) {
            // 找到第一个参数最少的构造方法 TODO: 待改进
            var constructor = type.GetConstructors()
                                  .OrderByDescending(c => c.GetParameters().Length)
                                  .FirstOrDefault() ?? throw new InvalidOperationException($"No constructor found for type {type.FullName}.");
            var parameters = constructor.GetParameters();
            var parameterInstances = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++) {
                var parameterType = parameters[i].ParameterType;
                // 使用泛型方法 GetRequiredService 来解析依赖
                parameterInstances[i] = typeof(ObjectProvider)
                    .GetMethod(nameof(GetRequiredService), BindingFlags.Public | BindingFlags.Static)
                    ?.MakeGenericMethod(parameterType)
                    .Invoke(null, [lifetimeForParams, lifetimeForParams, scope]);
            }

            return (T)constructor.Invoke(parameterInstances);
        }

        private static readonly ConcurrentDictionary<Type, object> _singletonInstances = new();
        private static readonly ConcurrentDictionary<Type, Type> _relations = new();
        private static readonly ConditionalWeakTable<object, Dictionary<Type, object>> _scopedInstances = [];
        #endregion

        #region with timespan
        public static void RegisterRelationWithTimestamp<TInterface, TImplementation>()
            where TImplementation : TInterface, new() {
            _timestampRelations.TryAdd(typeof(TInterface), typeof(TImplementation));
        }

        public static T GetRequiredServiceWithTimestamp<T>(
            long timestamp,
            ObjectLifetime lifetime = ObjectLifetime.Transient,
            ObjectLifetime lifetimeForParams = ObjectLifetime.Transient,
            object? scope = null) {
            var type = typeof(T);

            // 接口 → 实现类型
            if (type.IsInterface || type.IsAbstract) {
                if (!_timestampRelations.TryGetValue(type, out var implType))
                    throw new InvalidOperationException(
                        $"No implementation found for service type {type.FullName}.");

                type = implType;
            }

            var key = (timestamp, type);

            switch (lifetime) {
                case ObjectLifetime.Singleton:
                    return (T)_timestampSingletons.GetOrAdd(
                        key,
                        _ => CreateInstanceWithTimestamp<T>(timestamp, type, lifetimeForParams, scope));

                case ObjectLifetime.Scoped:
                    if (scope == null)
                        throw new InvalidOperationException("Scope cannot be null for scoped services.");

                    var scopedDict = _timestampScopedInstances.GetOrCreateValue(scope);

                    if (!scopedDict.TryGetValue(key, out object value)) {
                        value = CreateInstanceWithTimestamp<T>(timestamp, type, lifetimeForParams, scope);
                        scopedDict[key] = value;
                    }

                    return (T)value;

                case ObjectLifetime.Transient:
                default:
                    return CreateInstanceWithTimestamp<T>(timestamp, type, lifetimeForParams, scope);
            }
        }

        /// <summary>
        /// 创建实例（完全复刻 CreateInstance）
        /// 只是多传一个 timestamp
        /// </summary>
        private static T CreateInstanceWithTimestamp<T>(
            long timestamp,
            Type type,
            ObjectLifetime lifetimeForParams,
            object scope) {
            var constructor = type.GetConstructors()
                                  .OrderByDescending(c => c.GetParameters().Length)
                                  .FirstOrDefault()
                               ?? throw new InvalidOperationException(
                                   $"No constructor found for type {type.FullName}.");

            var parameters = constructor.GetParameters();
            var instances = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++) {
                var pType = parameters[i].ParameterType;
                instances[i] = typeof(ObjectProvider)
                    .GetMethod(nameof(GetRequiredServiceWithTimestamp),
                               BindingFlags.Public | BindingFlags.Static)?
                    .MakeGenericMethod(pType)
                    .Invoke(null, new object[] { timestamp, lifetimeForParams, lifetimeForParams, scope });
            }

            return (T)constructor.Invoke(instances);
        }

        /// <summary>
        /// 清理所有 timestamp-key 的单例与 scoped 实例
        /// </summary>
        public static void CleanTimestamp() {
            // 清理单例
            foreach (var instance in _timestampSingletons.Values) {
                if (instance is IDisposable d) d.Dispose();
            }
            _timestampSingletons.Clear();

            // 清理 scope 实例
            foreach (var scoped in _timestampScopedInstances) {
                foreach (var instance in scoped.Value.Values) {
                    if (instance is IDisposable d) d.Dispose();
                }
            }
            _timestampScopedInstances.Clear();
        }

        private static readonly ConcurrentDictionary<Type, Type> _timestampRelations = new();
        private static readonly ConcurrentDictionary<(long timestamp, Type type), object> _timestampSingletons = new();
        private static readonly ConditionalWeakTable<object, Dictionary<(long timestamp, Type type), object>> _timestampScopedInstances = [];
        #endregion
    }
}
