using KeybindLib.Classes;
using UnityEngine;

namespace SemiKick
{
    public class SemiKickRunner : MonoBehaviour
    {
        private Keybind kickKeybind;
        private KickAnimHandler localPlayerHandler;
        private float kickCooldown = 1.4f;
        private float cooldownTimer = 0f;

        public void InitKey(Keybind keybind) => kickKeybind = keybind;

        public void SetLocalPlayer(KickAnimHandler handler)
        {
            localPlayerHandler = handler;
            SemiKick.LogInfo($"SemiKickRunner.SetLocalPlayer: handler={(handler != null)}, Avatar={(handler != null && handler.Avatar != null ? handler.Avatar.name : "NULL")}");
        }

        void Update()
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }

            if (SemiFunc.InputDown(kickKeybind.inputKey))
            {
                if (cooldownTimer > 0f) return;

                // ВАЖНО: рейкаст+классификация теперь идут ПЕРВЫМ делом, а не
                // после PerformKick, как было раньше. Причина — стретч ноги
                // в KickAnimationPlayer (см. ApplyLegStretch) должен знать
                // точку попадания ДО старта анимации, а не постфактум.
                bool found = TryFindKickTarget(out RaycastHit hit, out KickTarget target);
                Vector3? stretchTargetWorldPos = found ? (Vector3?)hit.point : null;

                if (localPlayerHandler != null)
                {
                    localPlayerHandler.PerformKick(stretchTargetWorldPos);
                }
                else
                {
                    SemiKick.LogWarning("SemiKickRunner.Update: localPlayerHandler == null, PerformKick пропущен.");
                }

                ApplyKickEffects(found, hit, target);
            }
        }

        /// <summary>
        /// Реальный уровень апгрейда локального игрока (из
        /// KickAnimHandler.KickLevel, который наполняется REPOLib-колбэками
        /// в SemiKick.cs) + debug-добавка из SemiKickConfig.KickLevel.
        /// Так конфиг остаётся рабочим инструментом для теста баланса без
        /// похода в магазин, но не подменяет собой реальную прокачку.
        /// </summary>
        private int GetEffectiveKickLevel()
        {
            int realLevel = localPlayerHandler != null ? localPlayerHandler.KickLevel : 0;
            int debugAddon = SemiKickConfig.KickLevel.Value;
            int total = realLevel + debugAddon;

            if (debugAddon != 0)
            {
                SemiKick.LogInfo($"GetEffectiveKickLevel: realLevel={realLevel}, debugAddon={debugAddon} (из конфига), итог={total}.");
            }

            return total;
        }

        /// <summary>
        /// Рейкаст + классификация цели. Раньше это была первая часть
        /// DoPhysicsRaycast; вынесено отдельно, т.к. точку попадания теперь
        /// нужно получить ДО запуска анимации (для стретча ноги), а не
        /// после неё.
        /// </summary>
        private bool TryFindKickTarget(out RaycastHit hit, out KickTarget target)
        {
            hit = default;
            target = default;

            var ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
            var allHits = Physics.RaycastAll(ray, 2.3f);
            if (allHits.Length == 0)
            {
                SemiKick.LogInfo("TryFindKickTarget: рейкаст ни во что не попал.");
                return false;
            }

            System.Array.Sort(allHits, (a, b) => a.distance.CompareTo(b.distance));

            PlayerAvatar selfAvatar = localPlayerHandler != null ? localPlayerHandler.Avatar : null;

            foreach (var candidate in allHits)
            {
                SemiKick.LogInfo($"TryFindKickTarget: попадание в коллайдер '{candidate.collider.name}' на объекте '{candidate.collider.gameObject.name}'.");

                var candidateTarget = KickTargetClassifier.ClassifyHit(candidate.collider);
                SemiKick.LogInfo($"TryFindKickTarget: классификация -> Type={candidateTarget.Type}, Component={(candidateTarget.Component != null ? candidateTarget.Component.GetType().Name : "NULL")}");

                if (candidateTarget.Type == KickTargetType.Player
                    && selfAvatar != null
                    && ReferenceEquals(candidateTarget.Component, selfAvatar))
                {
                    SemiKick.LogInfo("TryFindKickTarget: попадание в СВОЕГО персонажа — игнорирую и продолжаю искать дальше.");
                    continue;
                }

                hit = candidate;
                target = candidateTarget;
                return true;
            }

            SemiKick.LogInfo("TryFindKickTarget: после пропуска своего персонажа других целей не найдено.");
            return false;
        }

        /// <summary>
        /// Всё, что раньше шло в DoPhysicsRaycast после нахождения цели:
        /// проверка на "устойчивую конструкцию", расчёт силы, тряска камеры
        /// и применение эффекта по типу цели (через RequestGenericKick).
        /// </summary>
        private void ApplyKickEffects(bool found, RaycastHit hit, KickTarget target)
        {
            if (!found)
            {
                SemiKick.LogInfo("ApplyKickEffects: цель не найдена, выхожу без эффектов.");
                return;
            }

            if (!InternalAccessors.OhGodDeveloper_WHATDIDIEVERDOTOYOU(localPlayerHandler?.Avatar))
            {
                SemiKick.LogInfo("Не устойчивая конструкция - пропуск.");
                return;
            }
            bool validTarget = target.Type == KickTargetType.Player
                || target.Type == KickTargetType.Enemy
                || target.Type == KickTargetType.Valuable;

            if (!validTarget)
            {
                SemiKick.LogInfo("ApplyKickEffects: цель не валидна (None/неизвестный тип), выхожу без эффектов.");
                return;
            }

            Vector3 direction = Camera.main.transform.forward;

            int effectiveLevel = GetEffectiveKickLevel();
            float force = KickForceCalculator.GetBaseKickForce(
                baseForce: SemiKickConfig.BaseForce.Value,
                kickLevel: effectiveLevel,
                levelMultiplier: SemiKickConfig.LevelMultiplier.Value);

            SemiKick.LogInfo($"ApplyKickEffects: рассчитанная сила force={force} (baseForce={SemiKickConfig.BaseForce.Value}, effectiveLevel={effectiveLevel}, levelMultiplier={SemiKickConfig.LevelMultiplier.Value})");

            float shakeStrength = Mathf.Clamp(
                force * SemiKickConfig.ShakeForceMultiplier.Value,
                SemiKickConfig.ShakeMin.Value,
                SemiKickConfig.ShakeMax.Value);

            if (GameDirector.instance != null && GameDirector.instance.CameraShake != null)
            {
                SemiKick.LogInfo($"ApplyKickEffects: вызываю CameraShake.Shake(strength={shakeStrength}, time={SemiKickConfig.ShakeTime.Value})");
                GameDirector.instance.CameraShake.Shake(shakeStrength, SemiKickConfig.ShakeTime.Value);
            }
            else
            {
                SemiKick.LogWarning("ApplyKickEffects: GameDirector.instance или CameraShake == null, тряска пропущена.");
            }

            PlayerAvatar kicker = localPlayerHandler != null ? localPlayerHandler.Avatar : null;
            if (kicker == null)
            {
                SemiKick.LogWarning("ApplyKickEffects: kicker (Avatar локального игрока) == null — knockback работать не будет для этого пинка.");
            }

            switch (target.Type)
            {
                case KickTargetType.Player:
                    SemiKick.LogInfo("ApplyKickEffects: цель Player -> KickNetworking.ApplyKickToPlayer (без self-knockback).");
                    KickNetworking.ApplyKickToPlayer((PlayerAvatar)target.Component, direction * force);
                    break;

                case KickTargetType.Enemy:
                    var enemy = (Enemy)target.Component;
                    var enemyReceiver = enemy.GetComponent<EnemyKickReceiver>();
                    if (enemyReceiver != null)
                    {
                        float enemyMass = enemyReceiver.GetMass();
                        SemiKick.LogInfo($"ApplyKickEffects: цель Enemy '{enemy.name}', mass={enemyMass} -> ставлю в очередь.");

                        if (localPlayerHandler != null)
                        {
                            localPlayerHandler.RequestGenericKick(() =>
                            {
                                // ПРОВЕРКА ВНУТРИ: не исчез ли враг за 1.5 сек?
                                if (enemyReceiver == null || enemy == null)
                                {
                                    SemiKick.LogWarning("ApplyKickEffects: Враг исчез до момента удара!");
                                    return;
                                }

                                enemyReceiver.SendKick(direction * force);

                                // Проверяем, жив ли еще наш игрок
                                if (kicker != null)
                                    KnockbackCalculator.Apply(kicker, enemyMass, force, direction);
                            });
                        }
                        else
                        {
                            enemyReceiver.SendKick(direction * force);
                            KnockbackCalculator.Apply(kicker, enemyMass, force, direction);
                        }
                    }
                    break;

                case KickTargetType.Valuable:
                    var physGrabObject = (PhysGrabObject)target.Component;
                    var valuableReceiver = physGrabObject.GetComponent<ValuableKickReceiver>();
                    if (valuableReceiver != null)
                    {
                        float valuableMass = physGrabObject.rb != null ? physGrabObject.rb.mass : 0f;
                        Vector3 hitPoint = hit.point;

                        if (localPlayerHandler != null)
                        {
                            localPlayerHandler.RequestGenericKick(() =>
                            {
                                // ПРОВЕРКА ВНУТРИ: не исчез ли предмет за 1.5 сек?
                                if (valuableReceiver == null || physGrabObject == null)
                                {
                                    SemiKick.LogWarning("ApplyKickEffects: Предмет исчез до момента удара!");
                                    return;
                                }

                                valuableReceiver.RequestKick(direction * force, hitPoint);

                                // Проверяем, жив ли еще наш игрок и есть ли у предмета физ. тело
                                if (kicker != null && physGrabObject.rb != null)
                                    KnockbackCalculator.Apply(kicker, valuableMass, force, direction);
                            });
                        }
                        else
                        {
                            valuableReceiver.RequestKick(direction * force, hitPoint);
                            KnockbackCalculator.Apply(kicker, valuableMass, force, direction);
                        }
                    }
                    break;
            }
        }
    }
}