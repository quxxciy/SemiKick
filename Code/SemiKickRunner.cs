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
                if (localPlayerHandler != null)
                {
                    localPlayerHandler.PerformKick();
                }
                else
                {
                    SemiKick.LogWarning("SemiKickRunner.Update: localPlayerHandler == null, PerformKick пропущен.");
                }

                DoPhysicsRaycast();
            }
        }

        private void DoPhysicsRaycast()
        {
            var ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
            var allHits = Physics.RaycastAll(ray, 1.3f);
            if (allHits.Length == 0)
            {
                SemiKick.LogInfo("DoPhysicsRaycast: рейкаст ни во что не попал.");
                return;
            }

            System.Array.Sort(allHits, (a, b) => a.distance.CompareTo(b.distance));

            RaycastHit hit = default;
            KickTarget target = default;
            bool found = false;

            PlayerAvatar selfAvatar = localPlayerHandler != null ? localPlayerHandler.Avatar : null;

            foreach (var candidate in allHits)
            {
                SemiKick.LogInfo($"DoPhysicsRaycast: попадание в коллайдер '{candidate.collider.name}' на объекте '{candidate.collider.gameObject.name}'.");

                var candidateTarget = KickTargetClassifier.ClassifyHit(candidate.collider);
                SemiKick.LogInfo($"DoPhysicsRaycast: классификация -> Type={candidateTarget.Type}, Component={(candidateTarget.Component != null ? candidateTarget.Component.GetType().Name : "NULL")}");

                if (candidateTarget.Type == KickTargetType.Player
                    && selfAvatar != null
                    && ReferenceEquals(candidateTarget.Component, selfAvatar))
                {
                    SemiKick.LogInfo("DoPhysicsRaycast: попадание в СВОЕГО персонажа — игнорирую и продолжаю искать дальше.");
                    continue;
                }

                hit = candidate;
                target = candidateTarget;
                found = true;
                break;
            }

            if (!found)
            {
                SemiKick.LogInfo("DoPhysicsRaycast: после пропуска своего персонажа других целей не найдено.");
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
                SemiKick.LogInfo("DoPhysicsRaycast: цель не валидна (None/неизвестный тип), выхожу без эффектов.");
                return;
            }

            Vector3 direction = Camera.main.transform.forward;
            float force = KickForceCalculator.GetBaseKickForce(
                baseForce: SemiKickConfig.BaseForce.Value,
                kickLevel: SemiKickConfig.KickLevel.Value,
                levelMultiplier: SemiKickConfig.LevelMultiplier.Value);

            SemiKick.LogInfo($"DoPhysicsRaycast: рассчитанная сила force={force} (baseForce={SemiKickConfig.BaseForce.Value}, kickLevel={SemiKickConfig.KickLevel.Value}, levelMultiplier={SemiKickConfig.LevelMultiplier.Value})");

            float shakeStrength = Mathf.Clamp(
                force * SemiKickConfig.ShakeForceMultiplier.Value,
                SemiKickConfig.ShakeMin.Value,
                SemiKickConfig.ShakeMax.Value);

            if (GameDirector.instance != null && GameDirector.instance.CameraShake != null)
            {
                SemiKick.LogInfo($"DoPhysicsRaycast: вызываю CameraShake.Shake(strength={shakeStrength}, time={SemiKickConfig.ShakeTime.Value})");
                GameDirector.instance.CameraShake.Shake(shakeStrength, SemiKickConfig.ShakeTime.Value);
            }
            else
            {
                SemiKick.LogWarning("DoPhysicsRaycast: GameDirector.instance или CameraShake == null, тряска пропущена.");
            }

            PlayerAvatar kicker = localPlayerHandler != null ? localPlayerHandler.Avatar : null;
            if (kicker == null)
            {
                SemiKick.LogWarning("DoPhysicsRaycast: kicker (Avatar локального игрока) == null — knockback работать не будет для этого пинка.");
            }

            switch (target.Type)
            {
                case KickTargetType.Player:
                    SemiKick.LogInfo("DoPhysicsRaycast: цель Player -> KickNetworking.ApplyKickToPlayer (без self-knockback).");
                    KickNetworking.ApplyKickToPlayer((PlayerAvatar)target.Component, direction * force);
                    break;

                case KickTargetType.Enemy:
                    var enemy = (Enemy)target.Component;
                    var enemyReceiver = enemy.GetComponent<EnemyKickReceiver>();
                    if (enemyReceiver != null)
                    {
                        float enemyMass = enemyReceiver.GetMass();
                        SemiKick.LogInfo($"DoPhysicsRaycast: цель Enemy '{enemy.name}', mass={enemyMass} -> ставлю в очередь через RequestGenericKick (задержка 1.5с).");

                        // Как и с Player — сначала отключаем контроллер и ждём
                        // 1.5с (замах), и только потом реально шлём SendKick и
                        // считаем нокбэк. Раньше это применялось мгновенно.
                        if (localPlayerHandler != null)
                        {
                            localPlayerHandler.RequestGenericKick(() =>
                            {
                                enemyReceiver.SendKick(direction * force);
                                KnockbackCalculator.Apply(kicker, enemyMass, force, direction);
                            });
                        }
                        else
                        {
                            SemiKick.LogWarning("DoPhysicsRaycast: localPlayerHandler == null, нет корутины с задержкой — применяю Enemy-пинок сразу как фоллбэк.");
                            enemyReceiver.SendKick(direction * force);
                            KnockbackCalculator.Apply(kicker, enemyMass, force, direction);
                        }
                    }
                    else
                    {
                        SemiKick.LogWarning($"DoPhysicsRaycast: у Enemy '{enemy.name}' нет EnemyKickReceiver, пинок проигнорирован.");
                    }
                    break;

                case KickTargetType.Valuable:
                    var physGrabObject = (PhysGrabObject)target.Component;
                    var valuableReceiver = physGrabObject.GetComponent<ValuableKickReceiver>();
                    if (valuableReceiver != null)
                    {
                        float valuableMass = physGrabObject.rb != null ? physGrabObject.rb.mass : 0f;
                        SemiKick.LogInfo($"DoPhysicsRaycast: цель Valuable '{physGrabObject.name}', mass={valuableMass} -> ставлю в очередь через RequestGenericKick (задержка 1.5с).");

                        // hit.point фиксируем сейчас (на момент раскаста), а не
                        // через 1.5с — цель за это время могла сдвинуться/
                        // измениться коллайдер, но точка попадания должна
                        // соответствовать МОМЕНТУ пинка, а не моменту применения.
                        Vector3 hitPoint = hit.point;

                        if (localPlayerHandler != null)
                        {
                            localPlayerHandler.RequestGenericKick(() =>
                            {
                                valuableReceiver.RequestKick(direction * force, hitPoint);
                                KnockbackCalculator.Apply(kicker, valuableMass, force, direction);
                            });
                        }
                        else
                        {
                            SemiKick.LogWarning("DoPhysicsRaycast: localPlayerHandler == null, нет корутины с задержкой — применяю Valuable-пинок сразу как фоллбэк.");
                            valuableReceiver.RequestKick(direction * force, hitPoint);
                            KnockbackCalculator.Apply(kicker, valuableMass, force, direction);
                        }
                    }
                    else
                    {
                        SemiKick.LogWarning($"DoPhysicsRaycast: у PhysGrabObject '{physGrabObject.name}' нет ValuableKickReceiver, пинок проигнорирован.");
                    }
                    break;
            }
        }
    }
}