using System;

#if UNITY_EDITOR || DEVELOPMENT_BUILD

namespace PuzzleAttack.RuntimeDebug
{
    /// <summary>
    /// Attribute to mark methods as debug console commands.
    /// The system will automatically discover and register these via reflection.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class DebugCommandAttribute : Attribute
    {
        public string CommandName { get; private set; }
        public string Description { get; private set; }
        public string Usage { get; private set; }

        public DebugCommandAttribute(string commandName, string description, string usage = "")
        {
            CommandName = commandName;
            Description = description;
            Usage = usage;
        }
    }

    /// <summary>
    /// Metadata about a registered command including its delegate and help info
    /// </summary>
    public class CommandMetadata
    {
        public string Name;
        public string Description;
        public string Usage;
        public Delegate Method;
        public Type[] ParameterTypes;
        public string[] ParameterNames;

        public CommandMetadata(string name, string description, string usage, Delegate method, 
            Type[] paramTypes, string[] paramNames)
        {
            Name = name;
            Description = description;
            Usage = usage;
            Method = method;
            ParameterTypes = paramTypes;
            ParameterNames = paramNames;
        }

        /// <summary>
        /// Generates a formatted help string for this command
        /// </summary>
        public string GetHelpText()
        {
            string help = $"<color=cyan>/{Name}</color> - {Description}\n";
            
            if (!string.IsNullOrEmpty(Usage))
            {
                help += $"  Usage: {Usage}\n";
            }
            else if (ParameterTypes.Length > 0)
            {
                help += "  Parameters: ";
                for (int i = 0; i < ParameterTypes.Length; i++)
                {
                    help += $"<{ParameterNames[i]}:{ParameterTypes[i].Name}>";
                    if (i < ParameterTypes.Length - 1) help += " ";
                }
                help += "\n";
            }
            
            return help;
        }
    }
}

#endif