using HarmonyLib;

namespace SemiKick
{
    /// <summary>
    /// Аналог EnemyKickReceiverPatch, но для PhysGrabObject. Добавляет
    /// ValuableKickReceiver на каждый объект с PhysGrabObject при его Awake,
    /// если такого компонента там ещё нет.
    ///
    /// ⚠️ Не проверено дословно, что именно "Awake" — верная точка для патча
    /// у PhysGrabObject (у Enemy это подтверждено плейтестом). Если receiver
    /// не появляется/появляется поздно (уже после первого возможного пинка) —
    /// свериться, нет ли у PhysGrabObject более подходящего Start()/OnEnable()
    /// момента инициализации (например, момента получения PhotonView).
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabObject), "Awake")]
    internal static class ValuableKickReceiverPatch
    {
        [HarmonyPostfix]
        private static void Postfix(PhysGrabObject __instance)
        {
            if (__instance.gameObject.GetComponent<ValuableKickReceiver>() == null)
            {
                __instance.gameObject.AddComponent<ValuableKickReceiver>();
            }
        }
    }
}
