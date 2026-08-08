using Photon.Pun;
using UnityEngine;

namespace SemiKick
{
    /// <summary>
    /// Вешается Harmony-патчем на тот же GameObject, где живёт PhysGrabObject
    /// (см. ValuableKickReceiverPatch). Нужен только для гостей: гость сам
    /// не имеет права применять AddForce (см. PhysicsGrabbingManipulation()
    /// игры — там жёсткая проверка на мастера), поэтому шлёт запрос мастеру
    /// через RPC, а мастер уже локально считает физику.
    ///
    /// Если текущий клиент сам мастер (или синглплеер) — RPC вообще не нужен,
    /// физика применяется напрямую в том же кадре.
    /// </summary>
    internal class ValuableKickReceiver : MonoBehaviourPun
    {
        private PhysGrabObject _physGrabObject;

        private void Awake()
        {
            _physGrabObject = GetComponent<PhysGrabObject>();
        }

        /// <summary>
        /// Вызывается из SemiKickRunner при пинке. Сам решает — применить
        /// физику локально (если мы мастер/синглплеер) или отправить запрос
        /// мастеру по сети.
        /// </summary>
        public void RequestKick(Vector3 force, Vector3 hitPoint)
        {
            if (SemiFunc.IsMasterClientOrSingleplayer())
            {
                // Мы и есть мастер (или синглплеер) — можно сразу, RPC не нужен.
                KickNetworking.ApplyKickToValuable(_physGrabObject, force, hitPoint);
                return;
            }

            // Мы гость — просим мастера применить силу за нас.
            // ValuableKickReceiver сам унаследован от MonoBehaviourPun, поэтому
            // у него уже есть собственный photonView (лениво находит PhotonView
            // на этом же GameObject через GetComponent). Traverse тут не нужен —
            // это не чужое приватное поле PhysGrabObject, а наш собственный доступ.
            var targetPhotonView = photonView;
            if (targetPhotonView == null) return;

            targetPhotonView.RPC(nameof(RequestKickRPC), RpcTarget.MasterClient, force, hitPoint);
        }

        [PunRPC]
        private void RequestKickRPC(Vector3 force, Vector3 hitPoint, PhotonMessageInfo _info = default)
        {
            // Выполняется только у мастера (RpcTarget.MasterClient), но
            // ApplyKickToValuable всё равно ещё раз проверяет
            // IsMasterClientOrSingleplayer() внутри — на всякий случай,
            // это дёшево и защищает от гонки, если мастер сменился между
            // отправкой и получением RPC.
            KickNetworking.ApplyKickToValuable(_physGrabObject, force, hitPoint);
        }
    }
}