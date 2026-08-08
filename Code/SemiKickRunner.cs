using KeybindLib.Classes;
using UnityEngine;

namespace SemiKick
{
    public class SemiKickRunner : MonoBehaviour
    {
        private Keybind kickKeybind;
        private KickAnimHandler localPlayerHandler;

        public void InitKey(Keybind keybind) => kickKeybind = keybind;

        public void SetLocalPlayer(KickAnimHandler handler)
        {
            localPlayerHandler = handler;
            SemiKick.Log.LogInfo($"[SemiKick] SemiKickRunner.SetLocalPlayer: handler={(handler != null)}, Avatar={(handler != null && handler.Avatar != null ? handler.Avatar.name : "NULL")}");
        }

        void Update()
        {
            if (SemiFunc.InputDown(kickKeybind.inputKey))
            {
                if (localPlayerHandler != null)
                {
                    localPlayerHandler.PerformKick();
                }
                else
                {
                    SemiKick.Log.LogWarning("[SemiKick] SemiKickRunner.Update: localPlayerHandler == null, PerformKick пропущен.");
                }

                DoPhysicsRaycast();
            }
        }

        private void DoPhysicsRaycast()
        {
            if (!Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out var hit, 3f))
            {
                SemiKick.Log.LogInfo("[SemiKick] DoPhysicsRaycast: рейкаст ни во что не попал.");
                return;
            }

            SemiKick.Log.LogInfo($"[SemiKick] DoPhysicsRaycast: попадание в коллайдер '{hit.collider.name}' на объекте '{hit.collider.gameObject.name}'.");

            var target = KickTargetClassifier.ClassifyHit(hit.collider);
            SemiKick.Log.LogInfo($"[SemiKick] DoPhysicsRaycast: классификация -> Type={target.Type}, Component={(target.Component != null ? target.Component.GetType().Name : "NULL")}");

            bool validTarget = target.Type == KickTargetType.Player
                || target.Type == KickTargetType.Enemy
                || target.Type == KickTargetType.Valuable;

            if (!validTarget)
            {
                SemiKick.Log.LogInfo("[SemiKick] DoPhysicsRaycast: цель не валидна (None/неизвестный тип), выхожу без эффектов.");
                return;
            }

            Vector3 direction = Camera.main.transform.forward;
            float force = KickForceCalculator.GetBaseKickForce(
                baseForce: SemiKickConfig.BaseForce.Value,
                kickLevel: SemiKickConfig.KickLevel.Value,
                levelMultiplier: SemiKickConfig.LevelMultiplier.Value);

            SemiKick.Log.LogInfo($"[SemiKick] DoPhysicsRaycast: рассчитанная сила force={force} (baseForce={SemiKickConfig.BaseForce.Value}, kickLevel={SemiKickConfig.KickLevel.Value}, levelMultiplier={SemiKickConfig.LevelMultiplier.Value})");

            float shakeStrength = Mathf.Clamp(
                force * SemiKickConfig.ShakeForceMultiplier.Value,
                SemiKickConfig.ShakeMin.Value,
                SemiKickConfig.ShakeMax.Value);

            if (GameDirector.instance != null && GameDirector.instance.CameraShake != null)
            {
                SemiKick.Log.LogInfo($"[SemiKick] DoPhysicsRaycast: вызываю CameraShake.Shake(strength={shakeStrength}, time={SemiKickConfig.ShakeTime.Value})");
                GameDirector.instance.CameraShake.Shake(shakeStrength, SemiKickConfig.ShakeTime.Value);
            }
            else
            {
                SemiKick.Log.LogWarning("[SemiKick] DoPhysicsRaycast: GameDirector.instance или CameraShake == null, тряска пропущена.");
            }

            PlayerAvatar kicker = localPlayerHandler != null ? localPlayerHandler.Avatar : null;
            if (kicker == null)
            {
                SemiKick.Log.LogWarning("[SemiKick] DoPhysicsRaycast: kicker (Avatar локального игрока) == null — knockback работать не будет для этого пинка.");
            }

            switch (target.Type)
            {
                case KickTargetType.Player:
                    SemiKick.Log.LogInfo("[SemiKick] DoPhysicsRaycast: цель Player -> KickNetworking.ApplyKickToPlayer (без self-knockback).");
                    KickNetworking.ApplyKickToPlayer((PlayerAvatar)target.Component, direction * force);
                    break;

                case KickTargetType.Enemy:
                    var enemy = (Enemy)target.Component;
                    var enemyReceiver = enemy.GetComponent<EnemyKickReceiver>();
                    if (enemyReceiver != null)
                    {
                        enemyReceiver.SendKick(direction * force);

                        float enemyMass = enemyReceiver.GetMass();
                        SemiKick.Log.LogInfo($"[SemiKick] DoPhysicsRaycast: цель Enemy '{enemy.name}', mass={enemyMass} -> вызываю KnockbackCalculator.Apply.");
                        KnockbackCalculator.Apply(kicker, enemyMass, force, direction);
                    }
                    else
                    {
                        SemiKick.Log.LogWarning($"[SemiKick] DoPhysicsRaycast: у Enemy '{enemy.name}' нет EnemyKickReceiver, пинок проигнорирован.");
                    }
                    break;

                case KickTargetType.Valuable:
                    var physGrabObject = (PhysGrabObject)target.Component;
                    var valuableReceiver = physGrabObject.GetComponent<ValuableKickReceiver>();
                    if (valuableReceiver != null)
                    {
                        valuableReceiver.RequestKick(direction * force, hit.point);

                        float valuableMass = physGrabObject.rb != null ? physGrabObject.rb.mass : 0f;
                        SemiKick.Log.LogInfo($"[SemiKick] DoPhysicsRaycast: цель Valuable '{physGrabObject.name}', mass={valuableMass} -> вызываю KnockbackCalculator.Apply.");
                        KnockbackCalculator.Apply(kicker, valuableMass, force, direction);
                    }
                    else
                    {
                        SemiKick.Log.LogWarning($"[SemiKick] DoPhysicsRaycast: у PhysGrabObject '{physGrabObject.name}' нет ValuableKickReceiver, пинок проигнорирован.");
                    }
                    break;
            }
        }
    }
}
