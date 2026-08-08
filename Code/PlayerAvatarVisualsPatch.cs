using HarmonyLib;
using UnityEngine;

namespace SemiKick
{
    // Патчим основной скрипт игрока, который есть и в сингле, и в мультиплеере
    [HarmonyPatch(typeof(PlayerAvatar), "Start")]
    public static class PlayerAvatar_Start_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerAvatar __instance)
        {
            // Этот лог должен появиться ДЛЯ КАЖДОГО PlayerAvatar в сцене,
            // даже для чужих (до проверки IsMine) — если его нет в консоли
            // вообще, патч не наложился Harmony'ем или Start не вызывается.
            SemiKick.Log.LogInfo($"[SemiKick] PlayerAvatar_Start_Patch.Postfix СРАБОТАЛ на объекте '{__instance.name}'.");

            if (__instance.photonView == null)
            {
                SemiKick.Log.LogWarning($"[SemiKick] PlayerAvatar_Start_Patch: photonView == null на '{__instance.name}', выхожу.");
                return;
            }

            bool isLocal = !SemiFunc.IsMultiplayer() || __instance.photonView.IsMine;
            SemiKick.Log.LogInfo($"[SemiKick] PlayerAvatar_Start_Patch: photonView.IsMine={__instance.photonView.IsMine}, IsMultiplayer={SemiFunc.IsMultiplayer()}, isLocal={isLocal} на '{__instance.name}'.");

            if (!isLocal) return;

            SemiKick.Log.LogInfo($"[SemiKick] Локальный игрок найден: {__instance.name}");

            var inputHandler = __instance.gameObject.GetComponent<KickAnimHandler>()
                ?? __instance.gameObject.AddComponent<KickAnimHandler>();

            var visuals = __instance.GetComponentInChildren<PlayerAvatarVisuals>();
            KickAnimationPlayer kickPlayer = null;

            if (visuals != null)
            {
                kickPlayer = visuals.gameObject.AddComponent<KickAnimationPlayer>();
                string pluginDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string jsonPath = System.IO.Path.Combine(pluginDir, "kick_animation.json");
                kickPlayer.Initialize(jsonPath, visuals.transform);
            }
            else
            {
                SemiKick.Log.LogWarning($"[SemiKick] PlayerAvatar_Start_Patch: PlayerAvatarVisuals не найден на '{__instance.name}', kickPlayer будет null.");
            }

            inputHandler.Initialize(kickPlayer, __instance);

            SemiKick.Log.LogInfo("[SemiKick] Инициализация локального игрока завершена.");
        }
    }
}