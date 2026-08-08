using UnityEngine;

namespace SemiKick
{
    internal enum KickTargetType
    {
        None,
        Player,
        Enemy,
        Valuable
    }

    internal struct KickTarget
    {
        public KickTargetType Type;
        public Rigidbody Rigidbody;
        public Component Component; // PlayerAvatar / Enemy / PhysGrabObject (для Valuable — всегда PhysGrabObject, не ValuableObject)
    }

    /// <summary>
    /// Определяет, во что попал рейкаст пинка, и достаёт нужный Rigidbody.
    /// Принцип: не хардкодим виды мобов/предметов — опираемся на базовые
    /// компоненты, которые уже расставлены игрой (компонентный подход).
    ///
    /// ВАЖНО про иерархию врагов (проверено через UnityExplorer на
    /// "Enemy - Slow Mouth"):
    ///   Enable
    ///     ├── Controller   (здесь Enemy, EnemyHealth, EnemyKickReceiver...)
    ///     └── Rigidbody    (здесь EnemyRigidbody, PhysGrabObject, коллайдер)
    /// Controller и Rigidbody — СИБЛИНГИ, а не предок/потомок. Поэтому
    /// GetComponentInParent<Enemy>() с коллайдера никогда не находит Enemy —
    /// он физически не лежит в родительской цепочке. GetComponentInParent
    /// находит PhysGrabObject (он на самом объекте с коллайдером) и по
    /// ошибке классифицирует моба как Valuable.
    /// Решение: ищем EnemyRigidbody (он на предке коллайдера), а у него
    /// берём internal-поле enemy — прямую ссылку на Enemy на Controller.
    /// </summary>
    internal static class KickTargetClassifier
    {
        public static KickTarget ClassifyHit(Collider col)
        {
            var result = new KickTarget { Type = KickTargetType.None };

            // 1. Игрок — самый специфичный случай
            var player = col.GetComponentInParent<PlayerAvatar>();
            if (player == null)
            {
                // На игроке PlayerAvatar сидит на "Player Avatar Controller", а
                // визуальные меши (голова, ноги и т.д.) — в отдельной ветке [RIG],
                // сиблинге относительно Player Avatar Controller (оба под Player
                // Visuals). GetComponentInParent с коллайдера меша это не находит —
                // тот же случай, что и с EnemyRigidbody/Enemy у мобов.
                // transform.root тут безопасен: PlayerAvatar(Clone) — уникальный
                // корень именно ЭТОГО игрока, не общий на всю сцену.
                player = col.transform.root.GetComponentInChildren<PlayerAvatar>();
            }
            if (player != null)
            {
                result.Type = KickTargetType.Player;
                result.Component = player;
                result.Rigidbody = InternalAccessors.GetPlayerRigidbody(player);
                return result;
            }

            // 2. Моб — проверяем ПЕРЕД Valuable/PhysGrabObject, т.к. у врагов
            // тоже висит PhysGrabObject на том же объекте, что и коллайдер
            // (см. комментарий класса про иерархию Controller/Rigidbody).
            var enemyRb = col.GetComponentInParent<EnemyRigidbody>();
            if (enemyRb != null)
            {
                var enemy = InternalAccessors.GetEnemyFromRigidbody(enemyRb);
                if (enemy != null)
                {
                    // не пинаем то, что уже деспавнится/мертво
                    if (enemy.CurrentState == EnemyState.Despawn)
                        return result; // None

                    result.Type = KickTargetType.Enemy;
                    result.Component = enemy;
                    result.Rigidbody = InternalAccessors.GetEnemyRigidbody(enemyRb);
                    return result;
                }
            }

            // 3. Ценный предмет — отдельный компонент поверх PhysGrabObject (композиция).
            // ⚠️ В Component кладём именно PhysGrabObject, а НЕ ValuableObject —
            // обе Valuable-ветки (эта и пункт 4) должны отдавать один и тот же
            // тип компонента, иначе каст (PhysGrabObject)target.Component в
            // SemiKickRunner падает с InvalidCastException для настоящих
            // ценностей (было воспроизведено на книжке — она ValuableObject,
            // тележка и дверь идут через ветку 4 и там всё было ок).
            // Мы сознательно не различаем ценность и обычный физ-объект на
            // уровне пинка — поведение одинаковое, см. обсуждение в чате.
            var valuable = col.GetComponentInParent<ValuableObject>();
            if (valuable != null)
            {
                var physGrab = valuable.GetComponent<PhysGrabObject>();
                if (physGrab == null)
                    return result; // на всякий случай — без PhysGrabObject толкать нечего

                if (physGrab.grabbed)
                    return result; // не пинаем то, что кто-то держит в руках

                result.Type = KickTargetType.Valuable;
                result.Component = physGrab;
                result.Rigidbody = physGrab.rb;
                return result;
            }

            // 4. Любой другой трогаемый объект (пропсы, тележки и т.п.)
            var grabbable = col.GetComponentInParent<PhysGrabObject>();
            if (grabbable != null)
            {
                if (grabbable.grabbed)
                    return result;

                // TODO: у мобов на объекте есть NotValuableObject — это
                // маркер игры "физический объект, но не ценность". Возможно
                // стоит явно фильтровать по нему здесь, а не полагаться
                // только на то, что Enemy-ветка (пункт 2) уже перехватила
                // мобов раньше. Пока не трогаем — работает через порядок
                // проверок, но если появится ещё один тип объекта с
                // PhysGrabObject+NotValuableObject, но не Enemy, он опять
                // упадёт сюда как Valuable.

                result.Type = KickTargetType.Valuable;
                result.Component = grabbable;
                result.Rigidbody = grabbable.rb;
                return result;
            }

            // Ничего не подошло — геометрия уровня, стены и т.п.
            return result;
        }
    }
}