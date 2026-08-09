using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using KeybindLib;
using KeybindLib.Classes;
using UnityEngine;
using UnityEngine.SceneManagement;
using REPOLib.Modules;
using REPOLib;
using REPOLib.Objects.Sdk; // ItemContent
using System.IO;
using System.Reflection;
using System.Linq;

namespace SemiKick
{
    [BepInPlugin("quxxciy.semikick", "SemiKick", "0.1.1")]
    [BepInDependency("bulletbot.keybindlib")]
    [BepInDependency(REPOLib.MyPluginInfo.PLUGIN_GUID, BepInDependency.DependencyFlags.HardDependency)]
    public class SemiKick : BaseUnityPlugin
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

            // --- Загружаем embedded .repobundle и достаём Item ---
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

        // Загрузка AssetBundle из embedded resource и извлечение Item
        private ItemContent LoadItemContentFromEmbeddedBundle(string resourceFileName, string itemContentAssetName)
        {
            var asm = Assembly.GetExecutingAssembly();

            string resourceName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(resourceFileName));

            if (resourceName == null)
            {
                LogError($"Embedded resource '{resourceFileName}' не найден.");
                return null;
            }

            Stream stream = asm.GetManifestResourceStream(resourceName);
            MemoryStream ms = new MemoryStream();
            stream.CopyTo(ms);
            stream.Dispose();

            var assetBundle = AssetBundle.LoadFromMemory(ms.ToArray());
            ms.Dispose();

            if (assetBundle == null)
            {
                LogError("Не удалось загрузить AssetBundle из памяти.");
                return null;
            }

            var itemContent = assetBundle.LoadAsset<ItemContent>(itemContentAssetName);
            if (itemContent == null)
            {
                LogError($"ItemContent '{itemContentAssetName}' не найден в бандле.");
            }

            return itemContent;
        }
        private ItemContent LoadItemContentFromFile(string bundleFileName, string itemContentAssetName)
        {
            string pluginFolder = Path.GetDirectoryName(Info.Location);
            string bundlePath = Path.Combine(pluginFolder, bundleFileName);

            Logger.LogInfo($"Ищу бандл по пути: {bundlePath}");

            if (!File.Exists(bundlePath))
            {
                Logger.LogError($"Файл бандла не найден по пути: {bundlePath}");
                return null;
            }

            var assetBundle = AssetBundle.LoadFromFile(bundlePath);
            if (assetBundle == null)
            {
                Logger.LogError("AssetBundle.LoadFromFile вернул null — файл повреждён или не тот формат.");
                return null;
            }

            Logger.LogInfo($"AssetBundle загружен. Ассеты внутри: {string.Join(", ", assetBundle.GetAllAssetNames())}");

            var itemContent = assetBundle.LoadAsset<ItemContent>(itemContentAssetName);
            if (itemContent == null)
                Logger.LogError($"ItemContent с именем '{itemContentAssetName}' НЕ найден в бандле.");
            else
                Logger.LogInfo("ItemContent успешно загружен!");

            return itemContent;
        }

        // Вызывается при старте игрока (LateStart)
        private static void OnUpgradeStart(PlayerAvatar player, int level)
        {
            if (level <= 0) return;

            LogInfo($"[Start] {player.name} имеет {UpgradeId} уровня {level}");

            // TODO: применить пассивный эффект при старте раунда/игрока
            // Например, если сила пинка хранится в компоненте на самом игроке:
            // var kickComp = player.GetComponent<YourKickComponent>();
            // kickComp.BaseForce = SemiKickConfig.BaseKickForce.Value + level * SemiKickConfig.ForcePerLevel.Value;
        }

        // Вызывается каждый раз, когда уровень меняется (апгрейд куплен/использован)
        private static void OnUpgradeApplied(PlayerAvatar player, int level)
        {
            LogInfo($"[Applied] {player.name} теперь имеет {UpgradeId} уровня {level}");

            // TODO: пересчитать стат при изменении уровня апгрейда
            // Пример формулы (раскомментируешь и подставишь свою):
            // float newForce = SemiKickConfig.BaseKickForce.Value + level * SemiKickConfig.ForcePerLevel.Value;
            // var kickComp = player.GetComponent<YourKickComponent>();
            // kickComp.BaseForce = newForce;

            // Если сила пинка — статическое/глобальное поле конфига (не per-player):
            // SemiKickConfig.BaseForce = 1.5f + level * 5f;
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

        #endregion
    }
}