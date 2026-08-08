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
        internal static ManualLogSource Log;
        private static bool runnerCreated = false;

        void Awake()
        {
            Log = Logger;
            kickKeybind = Keybinds.Bind("General", "Kick", "<Keyboard>/f");

            SemiKickConfig.Init(Config);

            Logger.LogInfo("SemiKick загружен, бинд зарегистрирован.");

            harmony = new Harmony("quxxciy.semikick");
            harmony.PatchAll();

            // не создаём Runner сразу — ждём первой реальной загрузки сцены
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (runnerCreated) return; // создаём только один раз

            Logger.LogInfo($"Первая сцена загружена: {scene.name}, создаю Runner.");

            var runnerObj = new GameObject("SemiKickRunner");
            Object.DontDestroyOnLoad(runnerObj);
            var runner = runnerObj.AddComponent<SemiKickRunner>();
            runner.InitKey(kickKeybind);

            runnerCreated = true;
            SceneManager.sceneLoaded -= OnSceneLoaded; // больше не нужно, отписываемся
        }
    }
}