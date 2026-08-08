using Photon.Pun;
using UnityEngine;

namespace SemiKick
{
    public class KickAnimHandler : MonoBehaviour
    {
        private KickAnimationPlayer _animPlayer;
        private PhotonView _photonView;
        private PlayerAvatar _avatar;
        private bool _isLocal;

        public PlayerAvatar Avatar => _avatar;

        public void Initialize(KickAnimationPlayer animPlayer, PlayerAvatar avatar)
        {
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

        // Этот метод вызывает только локальный игрок из Runner
        public void PerformKick()
        {
            Debug.Log($"[JSONAnimation] KickAnimHandler.PerformKick вызван для {(_avatar != null ? _avatar.name : "NULL")}: _animPlayer={(_animPlayer != null ? "не NULL" : "NULL")}, Multiplayer={GameManager.Multiplayer()}.");

            if (_animPlayer != null)
            {
                _animPlayer.PlayKick();
            }
            else
            {
                Debug.LogWarning($"[JSONAnimation] KickAnimHandler.PerformKick: _animPlayer == NULL — анимация физически не может проиграться, т.к. компонент не был передан при Initialize (см. PlayerAvatarVisualsPatch).");
            }

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
        /// </summary>
        public void RequestKick(Vector3 force)
        {
            if (SemiFunc.IsMasterClientOrSingleplayer())
            {
                // Мы и есть мастер (или синглплеер) — можно сразу
                _avatar.ForceImpulse(force);
                return;
            }

            if (_photonView == null)
            {
                SemiKick.LogWarning($"[SemiKick] KickAnimHandler.RequestKick: _photonView == null, пинок не отправлен.");
                return;
            }

            // Мы гость — просим мастера пнуть за нас
            _photonView.RPC(nameof(RequestKickRPC), RpcTarget.MasterClient, force);
        }

        [PunRPC]
        private void RequestKickRPC(Vector3 force, PhotonMessageInfo _info = default)
        {
            // Выполнится на компьютере мастера, на ЕГО копии этого же PlayerAvatar
            _avatar.ForceImpulse(force);
        }
    }
}