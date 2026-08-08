using BepInEx.Configuration;
using BepInEx.Logging;
using Photon.Pun;

namespace SemiKick
{
    // ВНИМАНИЕ: этот файл собран из известных полей (BaseForce/KickLevel/
    // LevelMultiplier из прогресс-документа) + новых полей для камеры и
    // knockback. Если в вашей реальной версии файла есть что-то ещё (другие
    // секции, комментарии, порядок) — сверьте вручную перед компиляцией,
    // у меня не было полной актуальной версии этого файла.
    internal static class SemiKickConfig
    {
        // Включить/выключить логи вообще
        public static ConfigEntry<bool> EnableLogging;

        // Порог уровня логов (Info, Debug, Error и т.д.)
        public static ConfigEntry<LogLevel> MinLogLevel;



        // --- Сила пинка (существующее) ---
        public static ConfigEntry<float> BaseForce;
        public static ConfigEntry<int> KickLevel;
        public static ConfigEntry<float> LevelMultiplier;

        // --- Камера / "сочность" ---
        public static ConfigEntry<float> ShakeForceMultiplier;
        public static ConfigEntry<float> ShakeMin;
        public static ConfigEntry<float> ShakeMax;
        public static ConfigEntry<float> ShakeTime;

        // --- Knockback / отдача ---
        public static ConfigEntry<float> KnockbackSoftThreshold;
        public static ConfigEntry<float> KnockbackHardThreshold;
        public static ConfigEntry<float> RecoilForceMultiplier;
        public static ConfigEntry<float> KnockbackHardMinForce;
        public static ConfigEntry<float> ImpactHurtWindow;
        public static ConfigEntry<int> ImpactHurtDamageBase;
        public static ConfigEntry<int> ImpactHurtDamageMax;

        public static void Init(ConfigFile config)
        {
            EnableLogging = config.Bind(
                "Logging",                  // Секция в .cfg
                "EnableLogging",            // Ключ
                true,                       // Дефолтное значение
                "Включить или отключить логирование мода" // Описание
            );

            MinLogLevel = config.Bind(
                "Logging",
                "MinLogLevel",
                LogLevel.Info,              // По умолчанию пишем от Info и выше
                "Минимальный уровень логов (Debug, Info, Warning, Error)"
            );

            BaseForce = config.Bind("Kick", "BaseForce", 10f, "Базовая сила пинка (до апгрейдов).");
            KickLevel = config.Bind("Kick", "KickLevel", 0, "Текущий уровень апгрейда силы пинка.");
            LevelMultiplier = config.Bind("Kick", "LevelMultiplier", 0.5f, "Множитель силы за уровень.");

            ShakeForceMultiplier = config.Bind("Camera", "ShakeForceMultiplier", 0.05f, "Множитель: сила пинка -> сила тряски камеры.");
            ShakeMin = config.Bind("Camera", "ShakeMin", 0.5f, "Минимальная тряска камеры при валидном пинке.");
            ShakeMax = config.Bind("Camera", "ShakeMax", 4f, "Максимальная тряска камеры (clamp).");
            ShakeTime = config.Bind("Camera", "ShakeTime", 0.05f, "Задержка до начала затухания тряски (CameraShake.time).");

            KnockbackSoftThreshold = config.Bind("Knockback", "SoftThreshold", 1.5f,
                "targetMass/kickForce выше этого значения -> лёгкая отдача без тамбла.");
            KnockbackHardThreshold = config.Bind("Knockback", "HardThreshold", 4f,
                "targetMass/kickForce выше этого значения -> тамбл + заряженный урон.");
            RecoilForceMultiplier = config.Bind("Knockback", "RecoilForceMultiplier", 3f,
                "Множитель self-impulse относительно МАССЫ ЦЕЛИ (не kickForce) — recoilForce = targetMass * этот множитель.");
            KnockbackHardMinForce = config.Bind("Knockback", "HardMinForce", 20f,
                "Гарантированный минимум силы self-impulse, когда сработал тамбл (перебивает computedForce, если тот меньше).");
            ImpactHurtWindow = config.Bind("Knockback", "ImpactHurtWindow", 0.5f,
                "Окно в секундах, в течение которого столкновение после тамбла наносит урон (tumble.ImpactHurtSet).");
            ImpactHurtDamageBase = config.Bind("Knockback", "ImpactHurtDamageBase", 15,
                "Базовый урон при превышении HardThreshold.");
            ImpactHurtDamageMax = config.Bind("Knockback", "ImpactHurtDamageMax", 50,
                "Максимальный урон при экстремальном превышении HardThreshold.");

            SemiKick.LogInfo("[SemiKick] SemiKickConfig.Init завершён. " +
                $"BaseForce={BaseForce.Value}, KickLevel={KickLevel.Value}, LevelMultiplier={LevelMultiplier.Value}, " +
                $"KnockbackSoft={KnockbackSoftThreshold.Value}, KnockbackHard={KnockbackHardThreshold.Value}");
        }
    }
}
