using Photon.Pun;
using UnityEngine;

namespace SemiKick
{
    /// <summary>
    /// Вешается на GameObject каждого Enemy через Harmony-патч на Enemy.Awake
    /// (см. EnemyKickReceiverPatch), ОДИНАКОВО на всех клиентах одного и того
    /// же сетевого объекта — иначе PUN не найдёт метод-приёмник у других игроков.
    ///
    /// Использует уже существующий PhotonView врага (Enemy.PhotonView),
    /// свой PhotonView не создаём.
    ///
    /// ВАЖНО: этот компонент вешается на тот же GameObject, что и Enemy —
    /// т.е. на "Controller". А EnemyRigidbody сидит на СИБЛИНГЕ "Rigidbody"
    /// (оба — дети "Enable"), поэтому GetComponent<EnemyRigidbody>() тут
    /// ничего не найдёт — ищем через общего родителя.
    /// </summary>
    internal class EnemyKickReceiver : MonoBehaviour
    {
        private const float KickStunDuration = 1f;

        private Enemy _enemy;
        private EnemyRigidbody _enemyRigidbody;
        private EnemyStateStunned _stateStunned;

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            _stateStunned = InternalAccessors.GetEnemyStateStunned(_enemy);

            _enemyRigidbody = transform.parent != null
                ? transform.parent.GetComponentInChildren<EnemyRigidbody>()
                : GetComponentInChildren<EnemyRigidbody>();

            if (_enemyRigidbody == null)
                SemiKick.LogWarning($"[SemiKick] EnemyKickReceiver на {name}: не нашёл EnemyRigidbody у сиблингов родителя.");
            if (_stateStunned == null)
                SemiKick.LogWarning($"[SemiKick] EnemyKickReceiver на {name}: не нашёл StateStunned — нокбэк будет гаситься AI на следующем кадре.");

            SemiKick.LogInfo($"[SemiKick] EnemyKickReceiver.Awake на {name}: enemyRigidbody={(_enemyRigidbody != null)}, stateStunned={(_stateStunned != null)}");
        }

        /// <summary>
        /// Масса Rigidbody врага. Возвращает 0, если не удалось достать —
        /// вызывающий код (KnockbackCalculator) обязан проверять на 0
        /// и не считать нулевую массу как "лёгкий враг".
        /// </summary>
        public float GetMass()
        {
            if (_enemyRigidbody == null)
            {
                SemiKick.LogWarning($"[SemiKick] EnemyKickReceiver.GetMass на {name}: _enemyRigidbody == null, возвращаю 0.");
                return 0f;
            }

            var rb = InternalAccessors.GetEnemyRigidbody(_enemyRigidbody);
            float mass = rb != null ? rb.mass : 0f;

            SemiKick.LogInfo($"[SemiKick] EnemyKickReceiver.GetMass на {name}: rb={(rb != null)}, mass={mass}");

            return mass;
        }

        public void SendKick(Vector3 force)
        {
            SemiKick.LogInfo($"[SemiKick] EnemyKickReceiver.SendKick на {name}: force={force}, magnitude={force.magnitude}, Multiplayer={GameManager.Multiplayer()}");

            if (!GameManager.Multiplayer())
            {
                ReceiveKickRPC(force);
                return;
            }

            var photonView = InternalAccessors.GetEnemyPhotonView(_enemy);
            if (photonView == null)
            {
                SemiKick.LogWarning($"[SemiKick] EnemyKickReceiver.SendKick на {name}: photonView == null, RPC не отправлен.");
                return;
            }

            photonView.RPC(nameof(ReceiveKickRPC), RpcTarget.All, force);
        }

        [PunRPC]
        public void ReceiveKickRPC(Vector3 force, PhotonMessageInfo _info = default)
        {
            SemiKick.LogInfo($"[SemiKick] EnemyKickReceiver.ReceiveKickRPC на {name}: force={force}");

            if (_enemyRigidbody == null)
            {
                SemiKick.LogWarning($"[SemiKick] EnemyKickReceiver.ReceiveKickRPC на {name}: _enemyRigidbody == null, импульс не применён.");
                return;
            }

            _stateStunned?.Set(KickStunDuration);
            _enemyRigidbody.FreezeForces(force, Vector3.zero);
        }
    }
}
