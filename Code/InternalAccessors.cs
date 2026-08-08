using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace SemiKick
{
    /// <summary>
    /// Единая точка доступа к internal/private полям Assembly-CSharp,
    /// до которых нет прямого доступа из мода (нет InternalsVisibleTo).
    /// Все Traverse-вызовы держим здесь, чтобы при поломке после апдейта игры
    /// править в одном месте, а не искать по всему проекту.
    /// </summary>
    internal static class InternalAccessors
    {
        // --- EnemyRigidbody ---
        // internal Rigidbody rb;
        public static Rigidbody GetEnemyRigidbody(EnemyRigidbody enemyRb)
        {
            if (enemyRb == null) return null;
            var rb = Traverse.Create(enemyRb).Field("rb").GetValue<Rigidbody>();
            if (rb == null)
                SemiKick.LogWarning("[SemiKick] InternalAccessors.GetEnemyRigidbody: поле 'rb' вернуло null.");
            return rb;
        }

        // internal Enemy enemy;  (ссылка обратно на владельца — Enemy сидит
        // на СИБЛИНГЕ (Controller), а не на предке коллайдера, поэтому
        // GetComponentInParent<Enemy>() с коллайдера её не находит)
        public static Enemy GetEnemyFromRigidbody(EnemyRigidbody enemyRb)
        {
            if (enemyRb == null) return null;
            return Traverse.Create(enemyRb).Field("enemy").GetValue<Enemy>();
        }

        // --- PlayerAvatar ---
        // private Rigidbody rb;
        public static Rigidbody GetPlayerRigidbody(PlayerAvatar player)
        {
            if (player == null) return null;
            return Traverse.Create(player).Field("rb").GetValue<Rigidbody>();
        }

        // internal bool isTumbling;
        public static bool GetIsTumbling(PlayerAvatar player)
        {
            if (player == null) return false;
            return Traverse.Create(player).Field("isTumbling").GetValue<bool>();
        }

        // internal PlayerTumble tumble;
        public static PlayerTumble GetTumbleComponent(PlayerAvatar player)
        {
            if (player == null)
            {
                SemiKick.LogWarning("[SemiKick] InternalAccessors.GetTumbleComponent: player == null.");
                return null;
            }

            var tumble = Traverse.Create(player).Field("tumble").GetValue<PlayerTumble>();
            if (tumble == null)
                SemiKick.LogWarning($"[SemiKick] InternalAccessors.GetTumbleComponent: поле 'tumble' вернуло null для {player.name}.");
            else
                SemiKick.LogInfo($"[SemiKick] InternalAccessors.GetTumbleComponent: tumble найден для {player.name}.");

            return tumble;
        }

        // --- Enemy ---
        // internal PhotonView PhotonView;  (именно так, с большой буквы в игре)
        public static PhotonView GetEnemyPhotonView(Enemy enemy)
        {
            if (enemy == null) return null;
            return Traverse.Create(enemy).Field("PhotonView").GetValue<PhotonView>();
        }

        // internal EnemyStateStunned StateStunned;
        public static EnemyStateStunned GetEnemyStateStunned(Enemy enemy)
        {
            if (enemy == null) return null;
            return Traverse.Create(enemy).Field("StateStunned").GetValue<EnemyStateStunned>();
        }

        // TODO: уточнить у слабой ИИ точную сигнатуру метода в PhysGrabber,
        // который сравнивает grabStrength игрока с массой объекта —
        // сюда добавить обёртку, когда будет найден.


        // Объявляем быстрые ссылки на internal-поля класса PlayerAvatar
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> IsTumblingRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isTumbling");

        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> IsCrouchingRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isCrouching");

        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> IsGroundedRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isGrounded");

        public static bool OhGodDeveloper_WHATDIDIEVERDOTOYOU(PlayerAvatar player)
        {
            if (player == null) return false;

            bool isTumbling = IsTumblingRef(player);
            bool isCrouching = IsCrouchingRef(player);
            bool isGrounded = IsGroundedRef(player);

            return !isTumbling && !isCrouching && isGrounded;
        }
    }
}
