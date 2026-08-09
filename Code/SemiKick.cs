using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using KeybindLib;
using KeybindLib.Classes;
using UnityEngine;
using UnityEngine.SceneManagement;
using REPOLib.Modules;
using REPOLib;

namespace SemiKick
{
    /// <summary>
    /// Точка входа плагина. Сама по себе — только bootstrap: инициализация
    /// конфига, бинда, Harmony-патчей и регистрация апгрейда в магазине.
    /// Остальная ответственность разнесена по partial-файлам:
    ///   - SemiKick.ItemBundle.cs — загрузка .repobundle / AssetBundle
    ///   - SemiKick.Upgrades.cs   — колбэки апгрейда силы пинка
    ///   - SemiKick.Logging.cs    — обёртки над логированием
    /// </summary>
    [BepInPlugin("quxxciy.semikick", "SemiKick", "0.1.1")]
    [BepInDependency("bulletbot.keybindlib")]
    [BepInDependency(REPOLib.MyPluginInfo.PLUGIN_GUID, BepInDependency.DependencyFlags.HardDependency)]
    public partial class SemiKick : BaseUnityPlugin
    {
        private Harmony harmony;
        private Keybind kickKeybind;
        private static bool runnerCreated = false;

        internal static ManualLogSource LoggerInstance;

        public const string UpgradeId = "SemiKick_KickUpgrade";

        void Awake()
        {
            LoggerInstance = Logger;

            SemiKickConfig.Init(Config);

            kickKeybind = Keybinds.Bind("General", "Kick", "<Keyboard>/f");

            LogInfo("SemiKick загружен, бинд зарегистрирован.");

            // --- Загружаем embedded .repobundle и достаём Item (см. SemiKick.ItemBundle.cs) ---
            var itemContent = LoadItemContentFromFile("semikick.repobundle", "REPOLib_Item Upgrade Kick");

            Item myItem = null;

            if (itemContent != null)
            {
                Items.RegisterItem(itemContent); // регистрация в магазине — сюда всё ещё ItemContent

                var attributes = itemContent.Prefab.GetComponent<ItemAttributes>();
                if (attributes != null)
                {
                    myItem = attributes.item; // ← вот тут достаём сам Item из компонента
                }
                else
                {
                    LogError("На префабе нет компонента ItemAttributes.");
                }
            }

            // startAction/upgradeAction — см. SemiKick.Upgrades.cs
            var upgrade = Upgrades.RegisterUpgrade(
                upgradeId: UpgradeId,
                item: myItem,          // теперь передаём Item, а не ItemAttributes
                startAction: OnUpgradeStart,
                upgradeAction: OnUpgradeApplied
            );

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
    }
}
