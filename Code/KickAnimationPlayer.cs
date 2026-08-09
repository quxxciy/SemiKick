using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;


[Serializable]
public class AnimationData
{
    public float duration;
    public List<FrameData> frames;
}

[Serializable]
public class FrameData
{
    public float time;
    public Dictionary<string, BoneRotation> bones;
}

[Serializable]
public struct BoneRotation
{
    public float x, y, z, w;

    /// <summary>
    /// Конвертация Blender -> Unity (Z-up правосторонняя -> Y-up левосторонняя)
    /// теперь делается один раз на этапе экспорта, в Python-скрипте
    /// (export_animation_for_unity.py, функция blender_quat_to_unity).
    /// Здесь просто собираем Quaternion из уже готовых значений.
    /// </summary>
    public Quaternion ToQuaternion()
    {
        return new Quaternion(x, y, z, w);
    }
}

// Оптимизированная структура для рантайма (без строк)
public struct RuntimeFrame
{
    public float time;
    public Quaternion[] rotations; // Индекс совпадает с индексом в кэше костей
}
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
        { "Bone", "ANIM BODY BOT" }, // Таз / тело
        { "Bone.003", "ANIM HEAD BOT" },   // Голова
        { "Bone.004", "Player Spring Impulse - Leg Right" },  // Правая нога
        { "Bone.005", "Player Spring Impulse - Leg Left" },  // Левая нога
        { "Bone.006", "Player Spring Impulse - Arm Left" },      // Левая рука
        { "Bone.007", "Player Spring Impulse - Arm Right" },      // Правая рука
    };

    // Кэш трансформов в виде массива для доступа по индексу (быстрее словаря)
    private Transform[] boneTransforms;
    // Кэш имен костей Blender, чтобы сопоставить их с индексами массива
    private string[] blenderBoneNames;

    private RuntimeFrame[] runtimeFrames;
    private float totalDuration;
    private bool isReady;
    private bool isPlaying;

    public void Initialize(string jsonPath, Transform rigRoot)
    {
        Debug.Log($"[JSONAnimation] Initialize вызван: jsonPath={jsonPath}, rigRoot={(rigRoot != null ? rigRoot.name : "NULL")}");

        // 1. Ищем только существующие кости и сохраняем их во временные списки
        var foundTransforms = new List<Transform>();
        var foundBlenderNames = new List<string>();

        foreach (var kvp in BoneMap)
        {
            Transform found = FindDeepChild(rigRoot, kvp.Value);
            if (found != null)
            {
                foundTransforms.Add(found);
                foundBlenderNames.Add(kvp.Key);
                Debug.Log($"[JSONAnimation] Привязана кость: blenderName={kvp.Key} -> unityName='{kvp.Value}', path={GetHierarchyPath(found)}");
            }
            else
            {
                Debug.LogWarning($"[JSONAnimation] НЕ найдена кость: blenderName={kvp.Key}, ожидалось имя в Unity='{kvp.Value}' (FindDeepChild не нашёл такого объекта под {(rigRoot != null ? rigRoot.name : "NULL")}).");
            }
        }

        // Фиксируем массивы под РЕАЛЬНОЕ количество найденных костей
        boneTransforms = foundTransforms.ToArray();
        blenderBoneNames = foundBlenderNames.ToArray();
        int actualBoneCount = boneTransforms.Length;

        Debug.Log($"[JSONAnimation] Итог поиска костей: найдено {actualBoneCount}/{BoneMap.Count}.");

        bool fileExists = File.Exists(jsonPath);
        Debug.Log($"[JSONAnimation] Проверка файла анимации: fileExists={fileExists}, path={jsonPath}");

        // 2. Загружаем и пересобираем данные
        if (fileExists && actualBoneCount > 0)
        {
            string text = File.ReadAllText(jsonPath);
            Debug.Log($"[JSONAnimation] JSON прочитан, длина текста={text.Length} символов. Десериализую...");

            var rawData = JsonConvert.DeserializeObject<AnimationData>(text);

            if (rawData == null)
            {
                Debug.LogError("[JSONAnimation] JsonConvert.DeserializeObject вернул NULL — файл битый или не соответствует структуре AnimationData. Анимация не будет готова.");
                return;
            }

            if (rawData.frames == null || rawData.frames.Count == 0)
            {
                Debug.LogError($"[JSONAnimation] rawData.frames пуст или NULL (duration={rawData.duration}). Анимация не будет готова.");
                return;
            }

            totalDuration = rawData.duration;
            runtimeFrames = new RuntimeFrame[rawData.frames.Count];

            Debug.Log($"[JSONAnimation] Десериализация ок: duration={totalDuration}, framesCount={rawData.frames.Count}. Пересобираю в RuntimeFrame...");

            int missingBoneSamples = 0;

            for (int f = 0; f < rawData.frames.Count; f++)
            {
                var sourceFrame = rawData.frames[f];
                runtimeFrames[f].time = sourceFrame.time;

                // Создаем массив ротаций только для найденных костей
                runtimeFrames[f].rotations = new Quaternion[actualBoneCount];

                for (int b = 0; b < actualBoneCount; b++)
                {
                    string bName = blenderBoneNames[b];
                    if (sourceFrame.bones != null && sourceFrame.bones.TryGetValue(bName, out BoneRotation rot))
                    {
                        runtimeFrames[f].rotations[b] = rot.ToQuaternion();
                    }
                    else
                    {
                        // Если кость есть в игре, но ее забыли анимировать в Blender, 
                        // используем текущий поворот, чтобы ее не "скрутило" в Quaternion.identity
                        runtimeFrames[f].rotations[b] = boneTransforms[b].localRotation;
                        missingBoneSamples++;
                    }
                }
            }

            if (missingBoneSamples > 0)
            {
                Debug.LogWarning($"[JSONAnimation] В {missingBoneSamples} случаях (кадр x кость) в JSON не было данных для найденной кости — использован текущий localRotation как фоллбэк. Если это не задумано, проверьте имена костей в Blender-экспорте.");
            }

            isReady = true;
            Debug.Log($"[JSONAnimation] Initialize завершён успешно: isReady=true, totalDuration={totalDuration}, framesCount={runtimeFrames.Length}, boneCount={actualBoneCount}.");
        }
        else
        {
            Debug.LogWarning($"[JSONAnimation] Initialize НЕ завершился (isReady останется false): fileExists={fileExists}, actualBoneCount={actualBoneCount}. " +
                (!fileExists ? "Файл kick_animation.json не найден по указанному пути. " : "") +
                (actualBoneCount == 0 ? "Ни одна кость из BoneMap не найдена в rigRoot — проверьте иерархию/имена." : ""));
        }
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null) return "NULL";
        string path = t.name;
        var current = t.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }

    private RuntimeFrame currentFrameA, currentFrameB;
    private float currentT;
    private bool hasFrameToApply;

    private IEnumerator RunKick()
    {
        isPlaying = true;
        float startTime = Time.time;
        int currentFrameIndex = 0;
        int frameCount = runtimeFrames.Length;
        int loggedFrameIndex = -1;

        Debug.Log($"[JSONAnimation] RunKick стартовал: startTime={startTime}, totalDuration={totalDuration}, frameCount={frameCount}, boneCount={boneTransforms?.Length ?? 0}.");

        while (Time.time - startTime < totalDuration)
        {
            float elapsed = Time.time - startTime;

            while (currentFrameIndex < frameCount - 1 &&
                   runtimeFrames[currentFrameIndex + 1].time <= elapsed)
            {
                currentFrameIndex++;
            }

            if (currentFrameIndex != loggedFrameIndex)
            {
                Debug.Log($"[JSONAnimation] RunKick: переход на кадр {currentFrameIndex}/{frameCount - 1} (time={runtimeFrames[currentFrameIndex].time}, elapsed={elapsed:F3}).");
                loggedFrameIndex = currentFrameIndex;
            }

            currentFrameA = runtimeFrames[currentFrameIndex];
            currentFrameB = runtimeFrames[Mathf.Min(currentFrameIndex + 1, frameCount - 1)];
            currentT = Mathf.InverseLerp(currentFrameA.time, currentFrameB.time, elapsed);
            hasFrameToApply = true;

            yield return null;
        }

        isPlaying = false;
        hasFrameToApply = false;

        Debug.Log($"[JSONAnimation] RunKick завершён: реальная длительность={Time.time - startTime:F3} (ожидалось totalDuration={totalDuration}).");
    }

    private bool _loggedFirstApply;
    private bool _loggedNullBoneWarning;

    private void LateUpdate()
    {
        if (!hasFrameToApply)
        {
            _loggedFirstApply = false;
            return;
        }

        if (!_loggedFirstApply)
        {
            Debug.Log($"[JSONAnimation] LateUpdate: начал применять ротации к {boneTransforms.Length} костям.");
            _loggedFirstApply = true;
        }

        for (int i = 0; i < boneTransforms.Length; i++)
        {
            if (boneTransforms[i] == null)
            {
                if (!_loggedNullBoneWarning)
                {
                    Debug.LogWarning($"[JSONAnimation] LateUpdate: boneTransforms[{i}] == NULL (кость уничтожена/недоступна?), пропускаю. Дальнейшие такие предупреждения на этот проигрыш подавлены.");
                    _loggedNullBoneWarning = true;
                }
                continue;
            }

            boneTransforms[i].localRotation = Quaternion.Slerp(
                currentFrameA.rotations[i],
                currentFrameB.rotations[i],
                currentT
            );
        }
    }

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
        Debug.Log($"[JSONAnimation] PlayKick вызван: isReady={isReady}, isPlaying={isPlaying}, gameObject={name}.");

        if (!isReady)
        {
            Debug.LogWarning($"[JSONAnimation] PlayKick: isReady=false, анимация не запущена (Initialize не завершился успешно — см. логи выше).");
            return;
        }

        if (isPlaying)
        {
            Debug.LogWarning($"[JSONAnimation] PlayKick: анимация уже проигрывается (isPlaying=true), повторный запуск пропущен.");
            return;
        }

        StartCoroutine(RunKick());
    }
}