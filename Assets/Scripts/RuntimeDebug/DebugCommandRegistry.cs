using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD

namespace PuzzleAttack.RuntimeDebug
{
    /// <summary>
    /// Automatically discovers and registers all methods decorated with [DebugCommand] attribute.
    /// Uses reflection to build a command registry for the console system.
    /// </summary>
    public static class DebugCommandRegistry
    {
        private static Dictionary<string, CommandMetadata> _commands = new Dictionary<string, CommandMetadata>();
        private static bool _initialized;

        public static IReadOnlyDictionary<string, CommandMetadata> Commands => _commands;

        /// <summary>
        /// Scans all assemblies for [DebugCommand] attributes and registers them
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            _commands.Clear();

            // Find all types in all assemblies
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            
            foreach (var assembly in assemblies)
            {
                // Skip Unity assemblies for performance
                if (assembly.FullName.StartsWith("Unity")) continue;
                if (assembly.FullName.StartsWith("System")) continue;
                if (assembly.FullName.StartsWith("mscorlib")) continue;

                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        RegisterCommandsInType(type);
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // Skip assemblies that can't be loaded
                }
            }

            _initialized = true;
            Debug.Log($"[DebugCommandRegistry] Registered {_commands.Count} commands");
        }

        /// <summary>
        /// Registers all methods with [DebugCommand] attribute in a given type
        /// </summary>
        private static void RegisterCommandsInType(Type type)
        {
            try
            {
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | 
                                             BindingFlags.Instance | BindingFlags.Static);

                foreach (var method in methods)
                {
                    try
                    {
                        var attribute = method.GetCustomAttribute<DebugCommandAttribute>();
                        if (attribute == null) continue;

                        // Get parameter info
                        var parameters = method.GetParameters();
                        var paramTypes = parameters.Select(p => p.ParameterType).ToArray();
                        var paramNames = parameters.Select(p => p.Name).ToArray();

                        // Create delegate
                        Delegate methodDelegate;
                        if (method.IsStatic)
                        {
                            methodDelegate = Delegate.CreateDelegate(
                                GetDelegateType(paramTypes), method);
                        }
                        else
                        {
                            // For instance methods, we need to find or create the instance
                            var instance = FindOrCreateInstance(type);
                            if (instance == null)
                            {
                                Debug.LogWarning(
                                    $"[DebugCommandRegistry] Could not create instance of {type.Name} for command {attribute.CommandName}");
                                continue;
                            }

                            methodDelegate = Delegate.CreateDelegate(
                                GetDelegateType(paramTypes), instance, method);
                        }

                        // Register command
                        var metadata = new CommandMetadata(
                            attribute.CommandName,
                            attribute.Description,
                            attribute.Usage,
                            methodDelegate,
                            paramTypes,
                            paramNames
                        );

                        _commands[attribute.CommandName] = metadata;
                    }
                    catch (TypeLoadException)
                    {
                        // Skip methods with type loading issues (ImGui version mismatches, etc)
                    }
                    catch (Exception e)
                    {
                        // Log but continue - don't let one bad method break everything
                        Debug.LogWarning($"[DebugCommandRegistry] Failed to register method {method.Name}: {e.Message}");
                    }
                }
            }
            catch (TypeLoadException)
            {
                // Skip entire type if it has type loading issues
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DebugCommandRegistry] Failed to scan type {type.Name}: {e.Message}");
            }
        }

        /// <summary>
        /// Finds existing instance or creates one for component types
        /// </summary>
        private static object FindOrCreateInstance(Type type)
        {
            // Check if it's a MonoBehaviour/Component
            if (typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                // Try to find existing instance
                var existing = UnityEngine.Object.FindFirstObjectByType(type);
                if (existing != null)
                    return existing;

                // Check if it's on DebugManager
                if (DebugManager.Instance != null)
                {
                    var component = DebugManager.Instance.GetComponent(type);
                    if (component != null)
                        return component;
                }

                return null;
            }

            // For regular classes, try to create instance
            try
            {
                return Activator.CreateInstance(type);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the appropriate delegate type for the parameter list
        /// </summary>
        private static Type GetDelegateType(Type[] paramTypes)
        {
            // For common cases, use built-in Action/Func types
            switch (paramTypes.Length)
            {
                case 0: return typeof(Action);
                case 1: return typeof(Action<>).MakeGenericType(paramTypes);
                case 2: return typeof(Action<,>).MakeGenericType(paramTypes);
                case 3: return typeof(Action<,,>).MakeGenericType(paramTypes);
                case 4: return typeof(Action<,,,>).MakeGenericType(paramTypes);
                default:
                    // For more parameters, would need custom delegate - limit for now
                    throw new NotSupportedException("Commands with more than 4 parameters not supported");
            }
        }

        /// <summary>
        /// Execute a command by name with string arguments
        /// </summary>
        public static bool TryExecuteCommand(string commandName, string[] args, out string result)
        {
            result = "";

            if (!_commands.TryGetValue(commandName, out var metadata))
            {
                result = $"Unknown command: {commandName}. Type /help for list of commands.";
                return false;
            }

            // Convert string arguments to proper types
            if (args.Length != metadata.ParameterTypes.Length)
            {
                result = $"Wrong number of arguments for /{commandName}\n{metadata.GetHelpText()}";
                return false;
            }

            try
            {
                object[] convertedArgs = new object[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    convertedArgs[i] = ConvertArgument(args[i], metadata.ParameterTypes[i]);
                }

                // Invoke the command
                metadata.Method.DynamicInvoke(convertedArgs);
                result = $"Executed: /{commandName} {string.Join(" ", args)}";
                return true;
            }
            catch (Exception e)
            {
                result = $"Error executing /{commandName}: {e.InnerException?.Message ?? e.Message}";
                return false;
            }
        }

        /// <summary>
        /// Convert a string argument to the target type
        /// </summary>
        private static object ConvertArgument(string arg, Type targetType)
        {
            if (targetType == typeof(string))
                return arg;

            if (targetType == typeof(int))
                return int.Parse(arg);

            if (targetType == typeof(float))
                return float.Parse(arg);

            if (targetType == typeof(bool))
                return bool.Parse(arg);

            // Add more type conversions as needed
            throw new NotSupportedException($"Conversion to {targetType.Name} not supported");
        }

        /// <summary>
        /// Get list of command names matching a prefix (for autocomplete)
        /// </summary>
        public static List<string> GetCommandsStartingWith(string prefix)
        {
            return _commands.Keys
                .Where(cmd => cmd.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(cmd => cmd)
                .ToList();
        }
    }
}

#endif