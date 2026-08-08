using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using KeybindLib;
using KeybindLib.Classes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SemiKick
{
    [BepInPlugin("quxxciy.semikick", "SemiKick", "0.1.1")]
    [BepInDependency("bulletbot.keybindlib")]
    public class SemiKick : BaseUnityPlugin
    {
        private Harmony harmony;
        private Keybind kickKeybind;
        private static bool runnerCreated = false;

        internal static ManualLogSource LoggerInstance;

        void Awake()
        {
            LoggerInstance = Logger;

            // Инициализируем конфиг ДО первых логов
            SemiKickConfig.Init(Config);

            kickKeybind = Keybinds.Bind("General", "Kick", "<Keyboard>/f");

            LogInfo("SemiKick загружен, бинд зарегистрирован.");

            harmony = new Harmony("quxxciy.semikick");
            harmony.PatchAll();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (runnerCreated) return;

            LogInfo($"Первая сцена загружена: {scene.name}, создаю Runner.");

            var runnerObj = new GameObject("SemiKickRunner");
            Object.DontDestroyOnLoad(runnerObj);
            var runner = runnerObj.AddComponent<SemiKickRunner>();
            runner.InitKey(kickKeybind);

            runnerCreated = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        #region Helper Logging Methods

        public static void Log(LogLevel level, object data)
        {
            // 1. Проверяем, включены ли логи
            if (!SemiKickConfig.EnableLogging.Value) return;

            // 2. Проверяем, проходит ли уровень лога по порогу
            if ((SemiKickConfig.MinLogLevel.Value & level) != 0 || level >= SemiKickConfig.MinLogLevel.Value)
            {
                LoggerInstance?.Log(level, data);
            }
        }

        public static void LogInfo(object data) => Log(LogLevel.Info, data);
        public static void LogDebug(object data) => Log(LogLevel.Debug, data);
        public static void LogWarning(object data) => Log(LogLevel.Warning, data);
        public static void LogError(object data) => Log(LogLevel.Error, data);

        #endregion
    }
}