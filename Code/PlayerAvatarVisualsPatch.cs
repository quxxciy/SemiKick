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
            Debug.Log($"[JSONAnimation] PlayerAvatar_Start_Patch.Postfix вызван для {__instance.name}, photonView={(__instance.photonView != null)}.");

            if (__instance.photonView == null) return;

            // Добавляем обработчик ВСЕМ (и себе, и чужим аватарам)
            var inputHandler = __instance.gameObject.GetComponent<KickAnimHandler>()
                        ?? __instance.gameObject.AddComponent<KickAnimHandler>();

            // Player Visuals (где живёт PlayerAvatarVisuals) и Player Avatar
            // Controller (где живёт сам __instance/PlayerAvatar) — СИБЛИНГИ,
            // оба висят на общем родителе PlayerAvatar(Clone). Тот же паттерн,
            // что и с Enemy/EnemyRigidbody и с mesh_head_top у KickTargetClassifier:
            // GetComponentInChildren с __instance не находит Player Visuals,
            // т.к. он не потомок, а сосед. Ищем через transform.root — он
            // уникален для каждого клона игрока, соседей не подцепит.
            var visuals = __instance.transform.root.GetComponentInChildren<PlayerAvatarVisuals>(true);
            Debug.Log($"[JSONAnimation] transform.root.GetComponentInChildren<PlayerAvatarVisuals>(true) на {__instance.name} (root={__instance.transform.root.name}): найдено={visuals != null}" +
                (visuals != null ? $", gameObject.activeSelf={visuals.gameObject.activeSelf}, activeInHierarchy={visuals.gameObject.activeInHierarchy}" : "") + ".");

            KickAnimationPlayer kickPlayer = null;

            if (visuals != null)
            {
                // Тоже добавляем всем, чтобы видеть анимации других игроков
                kickPlayer = visuals.gameObject.GetComponent<KickAnimationPlayer>()
                            ?? visuals.gameObject.AddComponent<KickAnimationPlayer>();

                string pluginDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string jsonPath = System.IO.Path.Combine(pluginDir, "kick_animation.json");

                Debug.Log($"[JSONAnimation] Вызываю kickPlayer.Initialize(jsonPath={jsonPath}, rigRoot={visuals.transform.name}) на объекте {visuals.gameObject.name}.");
                kickPlayer.Initialize(jsonPath, visuals.transform);
            }
            else
            {
                Debug.LogWarning($"[JSONAnimation] PlayerAvatarVisuals НЕ найден на {__instance.name} (или его детях) — kickPlayer останется NULL, анимация для этого аватара работать не будет.");
            }

            // Инициализируем. Внутри Initialize сама решит, регистрировать ли себя в Runner
            Debug.Log($"[JSONAnimation] Передаю kickPlayer={(kickPlayer != null ? "не NULL" : "NULL")} в KickAnimHandler.Initialize для {__instance.name}.");
            inputHandler.Initialize(kickPlayer, __instance);
        }
    }
}