using UnityEngine;

namespace SemiKick
{
    /// <summary>
    /// Считает "не хватило силы" по отношению массы цели к силе пинка,
    /// и в зависимости от порога либо ничего не делает, либо даёт лёгкую
    /// отдачу, либо тамбл + заряженный урон (через штатный путь игры
    /// PlayerTumble.TumbleRequest / ImpactHurtSet, урон применяется только
    /// если игрок реально во что-то врежется в течение ImpactHurtWindow).
    /// </summary>
    internal static class KnockbackCalculator
    {
        public static void Apply(PlayerAvatar kicker, float targetMass, float kickForce, Vector3 kickDirection)
        {
            SemiKick.LogInfo($"[SemiKick] KnockbackCalculator.Apply вызван: kicker={(kicker != null ? kicker.name : "NULL")}, targetMass={targetMass}, kickForce={kickForce}, direction={kickDirection}");

            if (kicker == null)
            {
                SemiKick.LogWarning("[SemiKick] KnockbackCalculator.Apply: kicker == null (localPlayerHandler.Avatar не был передан?), выхожу без эффекта.");
                return;
            }

            if (targetMass <= 0f)
            {
                SemiKick.LogWarning($"[SemiKick] KnockbackCalculator.Apply: targetMass={targetMass} <= 0, выхожу без эффекта (масса не найдена или объект и правда невесомый).");
                return;
            }

            if (kickForce <= 0f)
            {
                SemiKick.LogWarning($"[SemiKick] KnockbackCalculator.Apply: kickForce={kickForce} <= 0, выхожу без эффекта.");
                return;
            }

            float resistanceRatio = targetMass / kickForce;
            float soft = SemiKickConfig.KnockbackSoftThreshold.Value;
            float hard = SemiKickConfig.KnockbackHardThreshold.Value;

            SemiKick.LogInfo($"[SemiKick] KnockbackCalculator: resistanceRatio={resistanceRatio} (targetMass={targetMass} / kickForce={kickForce}), SoftThreshold={soft}, HardThreshold={hard}");

            if (resistanceRatio <= soft)
            {
                SemiKick.LogInfo("[SemiKick] KnockbackCalculator: resistanceRatio <= SoftThreshold -> без отдачи.");
                return;
            }

            Vector3 recoilDirection = -kickDirection.normalized;
            // Recoil считаем от МАССЫ ЦЕЛИ, а не от kickForce — иначе при
            // намеренно заниженном BaseForce (например, для теста порогов)
            // отдача остаётся мизерной, даже если resistanceRatio огромный.
            // Смысл: чем тяжелее то, что вы попытались пнуть, тем сильнее
            // вас откидывает назад, независимо от текущей силы пинка.
            float recoilForce = targetMass * SemiKickConfig.RecoilForceMultiplier.Value;

            if (resistanceRatio > hard)
            {
                // Раз уж тамбл сработал — полёт должен быть ЗАМЕТНЫМ вне
                // зависимости от того, насколько именно тяжёлой оказалась
                // конкретная цель (масса 4 не должна ощущаться прям сильно
                // слабее массы 9 — тамбл это уже "ты облажался", и это
                // должно вставлять одинаково эпично).
                float computedForce = targetMass * SemiKickConfig.RecoilForceMultiplier.Value;
                float recoilForceHard = Mathf.Max(computedForce, SemiKickConfig.KnockbackHardMinForce.Value);

                SemiKick.LogInfo($"[SemiKick] KnockbackCalculator: resistanceRatio > HardThreshold -> ТАМБЛ + урон. computedForce={computedForce}, floor={SemiKickConfig.KnockbackHardMinForce.Value}, итог recoilForce={recoilForceHard}, direction={recoilDirection}");

                var tumble = InternalAccessors.GetTumbleComponent(kicker);
                if (tumble != null)
                {
                    int damage = CalculateDamage(resistanceRatio, hard);
                    SemiKick.LogInfo($"[SemiKick] KnockbackCalculator: вызываю tumble.TumbleRequest(true, false) и tumble.ImpactHurtSet(window={SemiKickConfig.ImpactHurtWindow.Value}, damage={damage})");

                    tumble.TumbleRequest(_isTumbling: true, _playerInput: false);
                    tumble.ImpactHurtSet(SemiKickConfig.ImpactHurtWindow.Value, damage);
                }
                else
                {
                    SemiKick.LogWarning("[SemiKick] KnockbackCalculator: tumble == null, тамбл/урон НЕ применены, будет только импульс.");
                }

                SemiKick.LogInfo($"[SemiKick] KnockbackCalculator: вызываю kicker.ForceImpulse({recoilDirection * recoilForceHard})");
                kicker.ForceImpulse(recoilDirection * recoilForceHard);
            }
            else
            {
                float lightForce = recoilForce * 0.3f;
                SemiKick.LogInfo($"[SemiKick] KnockbackCalculator: Soft < resistanceRatio <= Hard -> лёгкая отдача без тамбла. force={lightForce}, direction={recoilDirection}");
                kicker.ForceImpulse(recoilDirection * lightForce);
            }
        }

        private static int CalculateDamage(float resistanceRatio, float hardThreshold)
        {
            float t = Mathf.InverseLerp(hardThreshold, hardThreshold * 2f, resistanceRatio);
            int damage = Mathf.RoundToInt(Mathf.Lerp(
                SemiKickConfig.ImpactHurtDamageBase.Value,
                SemiKickConfig.ImpactHurtDamageMax.Value,
                t));

            SemiKick.LogInfo($"[SemiKick] KnockbackCalculator.CalculateDamage: resistanceRatio={resistanceRatio}, t={t}, damage={damage}");

            return damage;
        }
    }
}
