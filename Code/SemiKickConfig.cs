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

        // --- Анимация (JSON из Blender) ---
        public static ConfigEntry<int> AnimationConversionMode;

        // --- Стретч кости правой ноги (аналог "дотягивания" руки в самой игре) ---
        public static ConfigEntry<float> LegStretchNaturalReach;
        public static ConfigEntry<float> LegStretchMaxMultiplier;
        public static ConfigEntry<int> LegStretchAxis;
        public static ConfigEntry<float> LegStretchLerpSpeed;

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

            BaseForce = config.Bind("Kick", "BaseForce", 0.70f, "Базовая сила пинка (до апгрейдов).");
            KickLevel = config.Bind("Kick", "KickLevel", 0, "Текущий уровень апгрейда силы пинка.");
            LevelMultiplier = config.Bind("Kick", "LevelMultiplier", 0.5f, "Множитель силы за уровень.");

            ShakeForceMultiplier = config.Bind("Camera", "ShakeForceMultiplier", 0.05f, "Множитель: сила пинка -> сила тряски камеры.");
            ShakeMin = config.Bind("Camera", "ShakeMin", 0.5f, "Минимальная тряска камеры при валидном пинке.");
            ShakeMax = config.Bind("Camera", "ShakeMax", 4f, "Максимальная тряска камеры (clamp).");
            ShakeTime = config.Bind("Camera", "ShakeTime", 0.05f, "Задержка до начала затухания тряски (CameraShake.time).");

            AnimationConversionMode = config.Bind("Animation", "ConversionMode", 1,
                "Конвертация осей кватерниона Blender->Unity, если анимация играет не в ту сторону " +
                "(например, толчок идёт назад вместо вперёд). Перебрать значения 0-7 и найти рабочее: " +
                "0=без изменений, 1=инверсия X, 2=инверсия Y, 3=инверсия Z, " +
                "4=инверсия X+Y, 5=инверсия X+Z, 6=инверсия Y+Z, 7=инверсия X+Y+Z. " +
                "Требует перезахода на уровень/переспавна аватара, т.к. читается один раз при Initialize.");

            LegStretchNaturalReach = config.Bind("LegStretch", "NaturalReach", 1.0f,
                "Дистанция (метры) от кости правой ноги до точки попадания, до которой стретч НЕ включается — " +
                "нога 'и так дотягивается'. Взято на глаз, не подтверждено анимацией/размером рига — подобрать на плейтесте.");
            LegStretchMaxMultiplier = config.Bind("LegStretch", "MaxMultiplier", 1.8f,
                "Максимальный множитель localScale по оси стретча (ограничивает, чтобы нога не улетала в бесконечность на дальних рейкастах).");
            LegStretchAxis = config.Bind("LegStretch", "Axis", 1,
                "По какой ЛОКАЛЬНОЙ оси кости 'Player Spring Impulse - Leg Right' растягиваем: 0=X, 1=Y, 2=Z. " +
                "НЕ проверено, какая ось у этой кости соответствует направлению 'вдоль ноги' — перебрать 0/1/2 на " +
                "плейтесте, как и с AnimationConversionMode. Неверная ось растянет ногу вбок/не туда.");
            LegStretchLerpSpeed = config.Bind("LegStretch", "LerpSpeed", 8f,
                "Скорость плавного перехода scale к целевому значению стретча (Mathf.Lerp * Time.deltaTime * это). " +
                "Больше — резче/быстрее реагирует на дистанцию, меньше — плавнее, но может не успеть за коротким пинком.");

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