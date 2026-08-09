namespace SemiKick
{
    /// <summary>
    /// kickForce(level) = baseForce * (1 + level * 0.5)
    /// Черновая формула для баланса на плейтестах.
    ///
    /// ПОДТВЕРЖДЕНО (дамп HurtCollider/EnemyRigidbody): деление силы на массу
    /// цели вручную не нужно — EnemyRigidbody.FreezeForces применяет
    /// накопленную силу через rb.AddForce(force, ForceMode.Impulse), а
    /// Impulse-режим сам учитывает массу Rigidbody. Раньше здесь был
    /// GetKickImpulse(strengthPlaceholder, kickMultiplier, targetMass) как
    /// плейсхолдер под пока-не-найденную формулу PhysGrabber — она была не
    /// нужна для этой ветки (Enemy), удалена. Если понадобится похожая
    /// логика для Valuable/PhysGrabber — проверять её актуальность заново,
    /// не переиспользовать вслепую.
    /// </summary>
    internal static class KickForceCalculator
    {
        public static float GetBaseKickForce(float baseForce, int kickLevel, float levelMultiplier)
        {
            // (было 1 до 10.08.26)
            return baseForce * (2 + kickLevel * levelMultiplier);
        }
    }
}
