using UnityEngine;

namespace SemiKick
{
    /// <summary>
    /// Обёртка над сетевым применением силы пинка.
    /// Игрок: используем готовый PlayerAvatar.ForceImpulse — он сам внутри
    /// дёргает photonView.RPC("ForceImpulseRPC", RpcTarget.All, _force),
    /// свой RPC не нужен.
    /// Enemy: не через этот класс — см. EnemyKickReceiver.SendKick (свой RPC,
    /// нужен из-за стан-логики, которая должна быть идентична у всех клиентов).
    /// Valuable/PhysGrabObject: master-авторитарная модель — RPC не нужен.
    /// Силу считает только мастер (или синглплеер), остальным клиентам физика
    /// доезжает сама через сетевую синхронизацию Rigidbody у PhysGrabObject
    /// (подтверждено: PhysicsGrabbingManipulation() у самой игры делает точно
    /// такую же проверку перед AddForce/AddTorque).
    /// </summary>
    internal static class KickNetworking
    {
        public static void ApplyKickToPlayer(PlayerAvatar player, Vector3 force)
        {
            player.ForceImpulse(force);
        }

        /// <summary>
        /// Собственно применение физики — только у мастера (или в синглплеере).
        /// Вызывается изнутри ValuableKickReceiver: либо напрямую (если текущий
        /// клиент уже мастер), либо через RequestKickRPC после сетевого запроса
        /// от гостя. Не вызывать напрямую из Runner — Runner не знает, мастер
        /// он или нет, этим занимается ValuableKickReceiver.RequestKick.
        /// </summary>
        public static void ApplyKickToValuable(PhysGrabObject physGrabObject, Vector3 force, Vector3 hitPoint)
        {
            if (physGrabObject == null) return;

            // Повторная проверка на всякий случай (защита от гонки, если этот
            // метод вызвали напрямую, а не через ValuableKickReceiver, либо
            // если мастер сменился между отправкой RPC и его получением).
            if (!SemiFunc.IsMasterClientOrSingleplayer()) return;

            var rb = physGrabObject.rb; // поле называется rb — свериться при CS1061
            if (rb == null) return;

            // AddForceAtPosition вместо AddForce — точка попадания рейкаста
            // даёт объекту немного torque "бесплатно", не только толчок по центру масс.
            rb.AddForceAtPosition(force, hitPoint, ForceMode.Impulse);
        }

        // TODO: ApplyKickToEnemy — не нужен, Enemy идёт через EnemyKickReceiver.SendKick
        //   напрямую, минуя этот класс (см. комментарий выше). Возможно, стоит
        //   унифицировать позже, но пока сознательно не трогаем.
    }
}
