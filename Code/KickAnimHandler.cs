using Photon.Pun;
using UnityEngine;
using System.Collections;
namespace SemiKick
{
    public class KickAnimHandler : MonoBehaviour
    {
        private KickAnimationPlayer _animPlayer;
        private PhotonView _photonView;
        private PlayerAvatar _avatar;
        private bool _isLocal;
        private PlayerController pc;
        public PlayerAvatar Avatar => _avatar;

        /// <summary>
        /// Реальный уровень апгрейда силы пинка ЭТОГО игрока (приходит из
        /// REPOLib Upgrades.RegisterUpgrade -> OnUpgradeStart/OnUpgradeApplied
        /// в SemiKick.cs, per-player, персистентно между раундами).
        /// НЕ путать с SemiKickConfig.KickLevel — тот теперь просто debug-
        /// добавка поверх этого значения, см. SemiKickRunner.GetEffectiveKickLevel.
        /// </summary>
        public int KickLevel { get; private set; } = 0;

        /// <summary>
        /// Может прийти РАНЬШЕ Initialize (если OnUpgradeStart/OnUpgradeApplied
        /// сработает для этого PlayerAvatar до того, как PlayerAvatarVisualsPatch
        /// успеет создать и проинициализировать KickAnimHandler) — level в этом
        /// случае просто сохранится в поле и будет использован сразу, порядок
        /// вызовов не важен.
        /// </summary>
        public void SetKickLevel(int level)
        {
            SemiKick.LogInfo($"[SemiKick] KickAnimHandler.SetKickLevel: {(_avatar != null ? _avatar.name : name)} -> level={level} (было {KickLevel}).");
            KickLevel = level;
        }

        public void Initialize(KickAnimationPlayer animPlayer, PlayerAvatar avatar)
        {
            pc = GameObject.FindFirstObjectByType<PlayerController>();
            if (pc == null ) SemiKick.LogWarning("[KickAnimHandler] PlayerController не найден на сцене!");
            _animPlayer = animPlayer;
            _avatar = avatar;

            SemiKick.LogInfo($"[SemiKick] KickAnimHandler.Initialize вызван, avatar={(avatar != null ? avatar.name : "NULL")}");
            SemiKick.LogInfo($"[JSONAnimation] KickAnimHandler.Initialize: animPlayer передан как {(animPlayer != null ? "не NULL" : "NULL")}.");

            if (avatar == null)
            {
                SemiKick.LogError("[SemiKick] KickAnimHandler.Initialize: avatar передана как NULL!");
                return;
            }

            _photonView = avatar.photonView ?? avatar.GetComponent<PhotonView>() ?? avatar.GetComponentInParent<PhotonView>();

            if (_photonView == null)
            {
                SemiKick.LogError($"[SemiKick] Не удалось найти PhotonView на объекте {avatar.name}!");
                return;
            }

            _isLocal = !SemiFunc.IsMultiplayer() || _photonView.IsMine;

            SemiKick.LogInfo($"[SemiKick] KickAnimHandler: PhotonView найден, IsMine={_photonView.IsMine}, IsMultiplayer={SemiFunc.IsMultiplayer()}, isLocal={_isLocal}, ViewID={_photonView.ViewID}");

            if (_isLocal)
            {
                var runner = FindObjectOfType<SemiKickRunner>();
                if (runner != null)
                {
                    runner.SetLocalPlayer(this);
                    SemiKick.LogInfo("[SemiKick] KickAnimHandler: локальный игрок зарегистрирован в SemiKickRunner, Avatar передан.");
                }
                else
                {
                    SemiKick.LogWarning("[SemiKick] SemiKickRunner не найден на сцене! Локальный Avatar не будет доступен для knockback.");
                }
            }

            SemiKick.LogInfo($"[SemiKick] KickAnimHandler успешно инициализирован (Local: {_isLocal}, ViewID: {_photonView.ViewID})");
        }

        // Этот метод вызывает только локальный игрок из Runner.
        // stretchTargetWorldPos — точка (hit.point рейкаста пинка), до
        // которой нога тянется стретчем в KickAnimationPlayer. SemiKickRunner
        // теперь обязан сначала сделать рейкаст/классификацию и только потом
        // звать PerformKick — иначе точки ещё не будет существовать.
        public void PerformKick(Vector3? stretchTargetWorldPos = null)
        {
            Debug.Log($"[JSONAnimation] KickAnimHandler.PerformKick вызван для {(_avatar != null ? _avatar.name : "NULL")}: _animPlayer={(_animPlayer != null ? "не NULL" : "NULL")}, Multiplayer={GameManager.Multiplayer()}, stretchTarget={(stretchTargetWorldPos.HasValue ? stretchTargetWorldPos.Value.ToString() : "NULL")}.");

            if (_animPlayer != null)
            {
                _animPlayer.PlayKick(stretchTargetWorldPos);
            }
            else
            {
                Debug.LogWarning($"[JSONAnimation] KickAnimHandler.PerformKick: _animPlayer == NULL — анимация физически не может проиграться, т.к. компонент не был передан при Initialize (см. PlayerAvatarVisualsPatch).");
            }

            // ⚠️ RPC_PlayKick намеренно без stretchTargetWorldPos — точка
            // попадания известна только кикеру (это его локальный рейкаст),
            // синхронизировать её отдельным полем в RPC не стали. У других
            // клиентов анимация проиграется БЕЗ стретча ноги (нога не будет
            // тянуться визуально для зрителей, только у самого кикера).
            if (_photonView != null && GameManager.Multiplayer())
            {
                _photonView.RPC(nameof(RPC_PlayKick), RpcTarget.Others);
            }
        }

        [PunRPC]
        public void RPC_PlayKick()
        {
            if (_animPlayer != null)
            {
                _animPlayer.PlayKick();
            }
        }

        /// <summary>
        /// Просит применить импульс к игроку, которого пнули. Игра разрешает
        /// ForceImpulseRPC только от мастера или от владельца аватара
        /// (см. MasterAndOwnerOnlyRPC в декомпиле PlayerAvatar.ForceImpulseRPC),
        /// поэтому если мы не мастер — просим применить мастера за нас,
        /// а не вызываем avatar.ForceImpulse напрямую.
        ///
        /// Теперь это просто частный случай RequestGenericKick — вся логика
        /// "отключить контроллер -> подождать 1.5с -> включить контроллер"
        /// живёт в DelayedKickCoroutine и общая для ЛЮБОГО типа цели
        /// (Player/Enemy/Valuable), см. RequestGenericKick.
        /// </summary>
        public void RequestKick(Vector3 force)
        {
            RequestGenericKick(() =>
            {
                if (SemiFunc.IsMasterClientOrSingleplayer())
                {
                    // Мы мастер (или синглплеер) — применяем напрямую
                    _avatar.ForceImpulse(force);
                }
                else if (_photonView != null)
                {
                    // Мы гость — просим мастера применить импульс
                    _photonView.RPC(nameof(RequestKickRPC), RpcTarget.MasterClient, force);
                }
                else
                {
                    SemiKick.LogWarning($"[SemiKick] KickAnimHandler.RequestKick: _photonView == null, пинок не отправлен.");
                }
            });
        }

        /// <summary>
        /// Универсальная точка входа для ЛЮБОГО пинка (Player/Enemy/Valuable):
        /// отключает PlayerController локального игрока на время замаха,
        /// ждёт SemiKickConfig-независимую фиксированную задержку в 1.5с
        /// (пока проигрывается анимация замаха), включает контроллер обратно
        /// и только после этого выполняет переданное действие — фактическое
        /// применение силы/RPC/нокбэка. Раньше через эту задержку шёл только
        /// пинок по игроку (RequestKick), Enemy и Valuable применялись
        /// мгновенно из SemiKickRunner — теперь все три ветки идут сюда,
        /// вызывающий код (SemiKickRunner) просто передаёт лямбду с реальным
        /// применением эффекта.
        /// </summary>
        public void RequestGenericKick(System.Action applyAction)
        {
            StartCoroutine(DelayedKickCoroutine(applyAction));
        }

        private IEnumerator DelayedKickCoroutine(System.Action applyAction)
        {
            if (pc == null)
            {
                SemiKick.LogWarning("[KickAnimHandler.DelayedKickCoroutine] PlayerController не найден на сцене! Если что сейчас мод ляжет НАХУЙ, хорошо?");
            }

            pc.enabled = false;
            yield return new WaitForSeconds(0.5f);

            float duration = 0.11f;
            float totalKick = 49.04f;
            float elapsed = 0f;
            float lastHeight = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Min(1f, elapsed / duration);

                float smoothedT = Mathf.Sin(t * Mathf.PI * 0.5f);
                float currentHeight = totalKick * smoothedT;

                float frameDelta = currentHeight - lastHeight;
                CameraAim.Instance.AdditiveAimY(-frameDelta);

                lastHeight = currentHeight;
                yield return null;
            }

            pc.enabled = true;
            applyAction?.Invoke();
        }
        [PunRPC]
        private void RequestKickRPC(Vector3 force, PhotonMessageInfo _info = default)
        {
            // Выполнится на компьютере мастера, на ЕГО копии этого же PlayerAvatar
            _avatar.ForceImpulse(force);
        }
    }
}