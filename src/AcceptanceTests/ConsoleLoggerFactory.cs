namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests
{
    using System;
    using Logging;

    class ConsoleLoggerFactory : ILoggerFactory
    {
        public ILog GetLogger(string name)
        {
            return new ConsoleLog(name);
        }

        public ILog GetLogger(Type type)
        {
            return new ConsoleLog(type.FullName);
        }

        class ConsoleLog : ILog
        {
            public ConsoleLog(string name, LogLevel level = LogLevel.Debug)
            {
                this.name = name;
                this.level = level;
            }

            public bool IsDebugEnabled => level <= LogLevel.Debug;

            public bool IsInfoEnabled => level <= LogLevel.Info;

            public bool IsWarnEnabled => level <= LogLevel.Warn;

            public bool IsErrorEnabled => level <= LogLevel.Error;

            public bool IsFatalEnabled => level <= LogLevel.Fatal;

            public void Debug(string message)
            {
                Log(message, LogLevel.Debug);
            }

            public void Debug(string message, Exception exception)
            {
                Log(message, LogLevel.Debug);
            }

            public void DebugFormat(string format, params object[] args)
            {
                Log(string.Format(format, args), LogLevel.Debug);
            }

            public void Info(string message)
            {
                Log(message, LogLevel.Info);
            }

            public void Info(string message, Exception exception)
            {
                Log($"{message} {exception}", LogLevel.Info);
            }

            public void InfoFormat(string format, params object[] args)
            {
                Log(string.Format(format, args), LogLevel.Info);
            }

            public void Warn(string message)
            {
                Log(message, LogLevel.Warn);
            }

            public void Warn(string message, Exception exception)
            {
                Log($"{message} {exception}", LogLevel.Warn);
            }

            public void WarnFormat(string format, params object[] args)
            {
                Log(string.Format(format, args), LogLevel.Warn);
            }

            public void Error(string message)
            {
                Log(message, LogLevel.Error);
            }

            public void Error(string message, Exception exception)
            {
                Log($"{message} {exception}", LogLevel.Error);
            }

            public void ErrorFormat(string format, params object[] args)
            {
                Log(string.Format(format, args), LogLevel.Error);
            }

            public void Fatal(string message)
            {
                Log(message, LogLevel.Fatal);
            }

            public void Fatal(string message, Exception exception)
            {
                Log(string.Format("{0} {1}", message, exception), LogLevel.Fatal);
            }

            public void FatalFormat(string format, params object[] args)
            {
                Log(string.Format(format, args), LogLevel.Fatal);
            }

            void Log(string message, LogLevel messageSeverity)
            {
                if (level > messageSeverity)
                {
                    return;
                }

                Console.WriteLine($"{name}: {message}");
            }

            readonly string name;
            readonly LogLevel level;
        }
    }
}