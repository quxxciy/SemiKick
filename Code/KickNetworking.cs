using UnityEngine;

namespace SemiKick
{
    /// <summary>
    /// Обёртка над сетевым применением силы пинка.
    /// Игрок: используем готовый PlayerAvatar.ForceImpulse — он сам внутри
    /// дёргает photonView.RPC("ForceImpulseRPC", RpcTarget.All, _force),
    /// свой RPC не нужен. С уровня SemiKickSettings.PlayerTumbleGuaranteeLevel
    /// дополнительно гарантированно тамблим ЦЕЛЬ пинка (не путать с
    /// self-recoil тамблом кикера из KnockbackCalculator) — см. forceTumble
    /// в ApplyKickToPlayer, ведём тем же master/owner-путём, что и force.
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
        /// <param name="forceTumble">
        /// Гарантированный тамбл ЦЕЛИ (не путать с self-recoil тамблом кикера
        /// из KnockbackCalculator — это другая, принудительная механика,
        /// см. SemiKickSettings.PlayerTumbleGuaranteeLevel). Прокидывается
        /// дальше в KickAnimHandler.RequestKick — там же и решается, как
        /// это безопасно применить с учётом master/owner-авторизации.
        /// </param>
        public static void ApplyKickToPlayer(PlayerAvatar player, Vector3 force, bool forceTumble = false)
        {
            var handler = player.gameObject.GetComponent<KickAnimHandler>();
            if (handler != null)
            {
                handler.RequestKick(force, forceTumble);
            }
            else
            {
                // fallback на случай, если по какой-то причине хендлера нет
                SemiKick.LogWarning($"[SemiKick] ApplyKickToPlayer: у {player.name} нет KickAnimHandler, пинок может не сработать у гостей.");
                player.ForceImpulse(force);
                // ⚠️ forceTumble здесь намеренно не обрабатывается — без
                // handler'а нет доступа к безопасному master/owner-пути
                // (см. KickAnimHandler.RequestKick), а бросать TumbleRequest
                // напрямую без авторизации — то же, чего мы избегаем везде
                // с ForceImpulse. Тамбл цели в этом fallback-случае просто
                // не сработает — это редкий путь (см. warning выше).
            }
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
