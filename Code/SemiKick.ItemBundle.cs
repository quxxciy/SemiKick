using REPOLib.Objects.Sdk; // ItemContent
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SemiKick
{
    /// <summary>
    /// Загрузка ItemContent для апгрейда из AssetBundle — либо с диска
    /// (основной путь, см. LoadItemContentFromFile), либо из embedded
    /// resource сборки (запасной вариант, сейчас не используется в Awake,
    /// но оставлен — вдруг понадобится вернуться к embedded-варианту).
    /// </summary>
    public partial class SemiKick
    {
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
    }
}
