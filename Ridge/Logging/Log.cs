#define LOGGING_ENABLED

using System;
using System.IO;

namespace Ridge.Logging
{
    /// <summary>
    /// Specifies a component to specify logging for
    /// </summary>
    [Flags,]
    public enum LogComponent
    {
        None = 0,
        CPU = 0x1,
        Memory = 0x2,
        VMemory = 0x4,
        IOBus = 0x8,
        Display = 0x10,
        FDLP = 0x20,
        

        All = 0x7fffffff
    }

    /// <summary>
    /// Specifies the type (or severity) of a given log message
    /// </summary>
    [Flags]
    public enum LogType
    {
        None = 0,
        Normal = 0x1,
        Warning = 0x2,
        Error = 0x4,
        Verbose = 0x8,
        All = 0x7fffffff
    }

    /// <summary>
    /// Provides basic functionality for logging messages of all types.
    /// </summary>
    public static class Log
    {
        static Log()
        {
            Enabled = false;
            _components = LogComponent.FDLP | LogComponent.CPU | LogComponent.Display;
            _type = LogType.All;
            _logIndex = 0;
        }

        public static LogComponent LogComponents
        {
            get { return _components; }
            set { _components = value; }
        }

        public static readonly bool Enabled;

#if LOGGING_ENABLED
        /// <summary>
        /// Logs a message without specifying type/severity for terseness;
        /// will not log if Type has been set to None.
        /// </summary>
        /// <param name="level"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public static void Write(LogComponent component, string message, params object[] args)
        {
            Write(LogType.Normal, component, message, args);
        }

        public static void Write(LogType type, LogComponent component, string message, params object[] args)
        {
            if ((_type & type) != 0 &&
                (_components & component) != 0)
            {
                //
                // My log has something to tell you...
                // TODO: color based on type, etc.
                Console.WriteLine(_logIndex.ToString() + ": " + component.ToString() + ": " + message, args);
                _logIndex++;
            }
        }
#else
        public static void Write(LogComponent component, string message, params object[] args)
        {
            
        }

        public static void Write(LogType type, LogComponent component, string message, params object[] args)
        {

        }

#endif

        private static LogComponent _components;
        private static LogType _type;
        private static long _logIndex;
    }
}
