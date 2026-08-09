using UnityEngine;

namespace SemiKick
{
    /// <summary>
    /// Колбэки апгрейда силы пинка (см. Upgrades.RegisterUpgrade в
    /// SemiKick.cs/Awake). Оба метода — просто разные точки входа REPOLib
    /// для одного и того же: OnUpgradeStart — при старте/спавне игрока
    /// (LateStart), OnUpgradeApplied — сразу когда апгрейд куплен/применён.
    /// В обоих случаях REPOLib присылает АКТУАЛЬНЫЙ уровень апгрейда
    /// конкретного player (per-player, работает для чужих аватаров тоже).
    /// </summary>
    public partial class SemiKick
    {
        private static void OnUpgradeStart(PlayerAvatar player, int level)
        {
            LogInfo($"[Start] {player.name} имеет {UpgradeId} уровня {level}");
            ApplyKickLevelToPlayer(player, level);
        }

        private static void OnUpgradeApplied(PlayerAvatar player, int level)
        {
            LogInfo($"[Applied] {player.name} теперь имеет {UpgradeId} уровня {level}");
            ApplyKickLevelToPlayer(player, level);
        }

        // Общая точка: кладём level в KickAnimHandler конкретного игрока.
        // GetComponent, а не GetComponentInParent/root — KickAnimHandler
        // вешается в PlayerAvatarVisualsPatch на тот же GameObject, что и
        // сам PlayerAvatar (см. PlayerAvatar_Start_Patch.Postfix).
        //
        // ⚠️ AddComponent на всякий случай: если OnUpgradeStart сработает
        // раньше, чем PlayerAvatar.Start успеет навесить KickAnimHandler
        // (порядок патчей/колбэков REPOLib не гарантирован), level всё
        // равно не потеряется — сохранится в свежесозданном компоненте,
        // а PlayerAvatarVisualsPatch потом просто найдёт уже существующий
        // компонент через свой "?? AddComponent" и не тронет KickLevel.
        private static void ApplyKickLevelToPlayer(PlayerAvatar player, int level)
        {
            if (player == null)
            {
                LogWarning("[SemiKick] ApplyKickLevelToPlayer: player == null.");
                return;
            }

            var handler = player.gameObject.GetComponent<KickAnimHandler>()
                        ?? player.gameObject.AddComponent<KickAnimHandler>();
            handler.SetKickLevel(level);
        }
    }
}
