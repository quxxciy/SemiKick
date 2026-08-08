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

            SemiKick.Log.LogInfo($"[SemiKick] KickAnimHandler.Initialize вызван, avatar={(avatar != null ? avatar.name : "NULL")}");

            if (avatar == null)
            {
                SemiKick.Log.LogError("[SemiKick] KickAnimHandler.Initialize: avatar передана как NULL!");
                return;
            }

            _photonView = avatar.photonView ?? avatar.GetComponent<PhotonView>() ?? avatar.GetComponentInParent<PhotonView>();

            if (_photonView == null)
            {
                SemiKick.Log.LogError($"[SemiKick] Не удалось найти PhotonView на объекте {avatar.name}!");
                return;
            }

            _isLocal = !SemiFunc.IsMultiplayer() || _photonView.IsMine;

            SemiKick.Log.LogInfo($"[SemiKick] KickAnimHandler: PhotonView найден, IsMine={_photonView.IsMine}, IsMultiplayer={SemiFunc.IsMultiplayer()}, isLocal={_isLocal}, ViewID={_photonView.ViewID}");

            if (_isLocal)
            {
                var runner = FindObjectOfType<SemiKickRunner>();
                if (runner != null)
                {
                    runner.SetLocalPlayer(this);
                    SemiKick.Log.LogInfo("[SemiKick] KickAnimHandler: локальный игрок зарегистрирован в SemiKickRunner, Avatar передан.");
                }
                else
                {
                    SemiKick.Log.LogWarning("[SemiKick] SemiKickRunner не найден на сцене! Локальный Avatar не будет доступен для knockback.");
                }
            }

            SemiKick.Log.LogInfo($"[SemiKick] KickAnimHandler успешно инициализирован (Local: {_isLocal}, ViewID: {_photonView.ViewID})");
        }

        // Этот метод вызывает только локальный игрок из Runner
        public void PerformKick()
        {
            if (_animPlayer != null && GameManager.Multiplayer())
            {
                _animPlayer.PlayKick();
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
    }
}
