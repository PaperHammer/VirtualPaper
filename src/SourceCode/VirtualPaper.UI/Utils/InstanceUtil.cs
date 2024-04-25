using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace VirtualPaper.UI.Utils
{
    public class InstanceUtil<T>
    {
        static InstanceUtil()
        {
            Assembly assembly = Assembly.Load("VirtualPaper.UI");
            _types = [.. assembly.GetTypes()];
            _instances = [];
        }

        public static Type TryGetTypeByName(string name)
        {
            Type type = _types.FirstOrDefault(t => t.Name == name);
            return type;
        }

        public static T TryGetInstanceByName(string name, string subName, params object[] args)
        {
            if (_instances.TryGetValue(name + subName, out T value)) return value;

            Type type = _types.FirstOrDefault(t => t.Name == name);

            ConstructorInfo ctor = type.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == args.Length);
            ParameterInfo[] parameters = ctor.GetParameters();
            object[] convertedArgs = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                convertedArgs[i] = Convert.ChangeType(args[i], parameters[i].ParameterType);
            }

            T instance = (T)ctor.Invoke(convertedArgs);
            _instances[name + subName] = instance;

            return instance;
        }

        private static readonly HashSet<Type> _types;
        private static readonly ConcurrentDictionary<string, T> _instances;
    }
}
