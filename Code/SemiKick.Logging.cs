using BepInEx.Logging;

namespace SemiKick
{
    /// <summary>
    /// Единая точка логирования мода. Все вызовы вида SemiKick.LogInfo(...)
    /// по всему проекту продолжают работать без изменений — partial class,
    /// имя типа то же самое, просто реализация лежит в отдельном файле.
    /// </summary>
    public partial class SemiKick
    {
        public static void Log(LogLevel level, object data)
        {
            if (!SemiKickConfig.EnableLogging.Value) return;

            if ((SemiKickConfig.MinLogLevel.Value & level) != 0 || level >= SemiKickConfig.MinLogLevel.Value)
            {
                LoggerInstance?.Log(level, data);
            }
        }

        public static void LogInfo(object data) => Log(LogLevel.Info, data);
        public static void LogDebug(object data) => Log(LogLevel.Debug, data);
        public static void LogWarning(object data) => Log(LogLevel.Warning, data);
        public static void LogError(object data) => Log(LogLevel.Error, data);
    }
}
