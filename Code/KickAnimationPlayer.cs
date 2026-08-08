using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json.Linq;

/// <summary>
/// Проигрывает анимацию, экспортированную из Blender в JSON, напрямую крутя
/// Transform'ы игровой рантайм-иерархии.
/// Поиск Transform делается РЕКУРСИВНО ПО ИМЕНИ один раз при инициализации.
/// </summary>
public class KickAnimationPlayer : MonoBehaviour
{
    // Карта соответствия: имя кости в Blender -> ИМЯ ТРАНСФОРМА В UNITY (без путей!)
    private static readonly Dictionary<string, string> BoneMap = new Dictionary<string, string>
    {
        { "Bone", "ANIM BODY BOT SCALE" }, // Таз / тело
        { "Bone.003", "ANIM HEAD BOT" },   // Голова
        { "Bone.004", "ANIM LEG R BOT" },  // Правая нога
        { "Bone.005", "ANIM LEG L BOT" },  // Левая нога
        { "Bone.006", "ANIM ARM L" },      // Левая рука
        { "Bone.007", "ANIM ARM R" },      // Правая рука
    };

    // Закэшированные ссылки: имя кости Blender -> реальный Transform в игре
    private readonly Dictionary<string, Transform> boneCache = new Dictionary<string, Transform>();

    private JObject animData;
    private bool isReady;
    private bool isPlaying;

    /// <summary>
    /// Вызывается один раз при спавне игрока.
    /// </summary>
    public void Initialize(string jsonPath, Transform rigRoot)
    {
        if (!GameManager.Multiplayer() && SemiFunc.PlayerGetAll().Count < 2) return;

        // 1) Ищем трансформы рекурсивно по имени среди всех дочерних объектов rigRoot
        foreach (var kvp in BoneMap)
        {
            string blenderBone = kvp.Key;
            string targetGameObjectName = kvp.Value;

            Transform foundTransform = FindDeepChild(rigRoot, targetGameObjectName);

            if (foundTransform != null)
            {
                boneCache[blenderBone] = foundTransform;
                Debug.Log($"[KickAnimationPlayer] Кость '{blenderBone}' успешно привязана к '{foundTransform.name}'");
            }
            else
            {
                Debug.LogWarning($"[KickAnimationPlayer] Не найден объект с именем '{targetGameObjectName}' для кости '{blenderBone}'!");
            }
        }

        // 2) Загружаем JSON с данными анимации один раз
        if (File.Exists(jsonPath))
        {
            string text = File.ReadAllText(jsonPath);
            animData = JObject.Parse(text);
            isReady = true;
        }
        else
        {
            Debug.LogError($"[KickAnimationPlayer] JSON не найден: {jsonPath}");
        }
    }

    /// <summary>
    /// Рекурсивный поиск дочернего Transform по точному совпадению имени.
    /// </summary>
    private Transform FindDeepChild(Transform parent, string targetName)
    {
        if (parent.name == targetName) return parent;

        foreach (Transform child in parent)
        {
            Transform result = FindDeepChild(child, targetName);
            if (result != null) return result;
        }

        return null;
    }

    public void PlayKick()
    {
        if (!isReady || isPlaying) return;
        StartCoroutine(RunKick());
    }

    private IEnumerator RunKick()
    {
        isPlaying = true;

        var frames = (JArray)animData["frames"];
        float duration = animData["duration"].Value<float>();

        float startTime = Time.time;
        int currentFrameIndex = 0;

        while (Time.time - startTime < duration)
        {
            float elapsed = Time.time - startTime;

            // Находим два соседних кадра для интерполяции
            while (currentFrameIndex < frames.Count - 1 &&
                   frames[currentFrameIndex + 1]["time"].Value<float>() <= elapsed)
            {
                currentFrameIndex++;
            }

            var frameA = frames[currentFrameIndex];
            var frameB = frames[Mathf.Min(currentFrameIndex + 1, frames.Count - 1)];

            float timeA = frameA["time"].Value<float>();
            float timeB = frameB["time"].Value<float>();
            float segmentLength = Mathf.Max(0.0001f, timeB - timeA);
            float t = Mathf.Clamp01((elapsed - timeA) / segmentLength);

            var bonesA = (JObject)frameA["bones"];
            var bonesB = (JObject)frameB["bones"];

            // Применяем поворот из кэша (0 поиска во время игры)
            foreach (var kvp in boneCache)
            {
                string boneName = kvp.Key;
                Transform boneTransform = kvp.Value;

                if (bonesA[boneName] == null || bonesB[boneName] == null) continue;

                Quaternion qA = ReadQuat(bonesA[boneName]);
                Quaternion qB = ReadQuat(bonesB[boneName]);

                boneTransform.localRotation = Quaternion.Slerp(qA, qB, t);
            }

            yield return null;
        }

        isPlaying = false;
    }

    private static Quaternion ReadQuat(JToken bone)
    {
        return new Quaternion(
            bone["x"].Value<float>(),
            bone["y"].Value<float>(),
            bone["z"].Value<float>(),
            bone["w"].Value<float>()
        );
    }
}