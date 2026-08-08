using HarmonyLib;

namespace SemiKick
{
    /// <summary>
    /// Вешает EnemyKickReceiver на GameObject каждого Enemy сразу после его
    /// Awake — одинаково на всех клиентах, т.к. Awake вызывается локально
    /// для каждого сетевого объекта у всех игроков (сам объект уже
    /// заспавнен через PUN к этому моменту, PhotonView на нём уже есть).
    ///
    /// Postfix, а не Prefix — чтобы поля самого Enemy (в т.ч. то, что нужно
    /// EnemyKickReceiver.Awake через GetComponent) успели проинициализироваться.
    /// </summary>
    [HarmonyPatch(typeof(Enemy), "Awake")]
    internal static class EnemyKickReceiverPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Enemy __instance)
        {
            if (__instance.GetComponent<EnemyKickReceiver>() != null)
                return; // уже повешен (на случай повторного Awake/патча)

            __instance.gameObject.AddComponent<EnemyKickReceiver>();
        }
    }
}
