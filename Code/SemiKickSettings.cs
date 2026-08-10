using BepInEx.Logging;

namespace SemiKick
{
    /// <summary>
    /// Единая точка правды по всем балансным числам мода. Раньше это были
    /// BepInEx ConfigEntry (SemiKickConfig.cs) — удобно для дебага на лету
    /// через .cfg, но конфиг больше не нужен: баланс отдебажен, а плоские
    /// константы читаются и правятся быстрее, чем лезть в .cfg-файл.
    ///
    /// Если понадобится снова гонять параметры вживую без пересборки —
    /// SemiKickConfig.cs остаётся в истории git, просто верните ConfigEntry
    /// обратно и переключите обращения. Пока это не требуется.
    /// </summary>
    internal static class SemiKickSettings
    {
        // --- Логирование ---
        public const bool EnableLogging = true;
        public const LogLevel MinLogLevel = LogLevel.Info;

        // --- Сила пинка ---
        public const float BaseForce = 1.4f;
        public const float LevelMultiplier = 0.7f; // насколько решает апгрейд

        /// <summary>
        /// С какого уровня апгрейда (включительно) пинок по ИГРОКУ
        /// гарантированно вводит цель в tumble, независимо от resistanceRatio
        /// (тот механизм — KnockbackCalculator — вообще про другое: он
        /// тамблит КИКЕРА как отдачу за слишком тяжёлую цель, а это —
        /// принудительный тамбл ЦЕЛИ пинка по игроку, отдельная механика).
        /// См. KickNetworking.ApplyKickToPlayer / KickAnimHandler.RequestKick.
        /// </summary>
        public const int PlayerTumbleGuaranteeLevel = 2;

        /// <summary>
        /// kickForce(level) = BaseForce * (1 + level * LevelMultiplier).
        /// Раньше жила отдельным классом KickForceCalculator — перенесена
        /// сюда же, к остальной математике баланса, чтобы не искать формулу
        /// в отдельном файле, когда меняешь BaseForce/LevelMultiplier рядом.
        /// </summary>
        public static float GetKickForce(int kickLevel)
        {
            return BaseForce * (1 + kickLevel * LevelMultiplier);
        }

        // --- Камера ---
        public const float ShakeForceMultiplier = 0.05f; // сила пинка -> сила тряски камеры
        public const float ShakeMin = 4f;               // минимальная тряска при валидном пинке
        public const float ShakeMax = 14f;                 // максимальная тряска (clamp)
        public const float ShakeTime = 0.05f;             // задержка до начала затухания тряски

        // --- Стретч кости правой ноги ("дотягивание", аналог руки в самой игре) ---
        public const float LegStretchNaturalReach = 0.5f;   // дистанция (м), до которой стретч не включается
        public const float LegStretchMaxMultiplier = 1.8f;  // потолок localScale по оси стретча
        public const int LegStretchAxis = 1;                // локальная ось кости: 0=X, 1=Y, 2=Z
        public const float LegStretchLerpSpeed = 8f;        // скорость плавного перехода scale

        // --- Knockback / отдача кикеру ---
        public const float KnockbackSoftThreshold = 0.9f; // targetMass/kickForce выше -> лёгкая отдача без тамбла
        public const float KnockbackHardThreshold = 4f;   // targetMass/kickForce выше -> тамбл + заряженный урон
        public const float RecoilForceMultiplier = 3f;    // self-impulse = targetMass * это (не от kickForce!)
        public const float KnockbackHardMinForce = 20f;   // гарантированный минимум self-impulse при тамбле
        public const float ImpactHurtWindow = 0.5f;       // окно (сек) после тамбла, в течение которого столкновение наносит урон
        public const int ImpactHurtDamageBase = 15;       // урон при превышении HardThreshold
        public const int ImpactHurtDamageMax = 50;        // урон при экстремальном превышении HardThreshold
    }
}
