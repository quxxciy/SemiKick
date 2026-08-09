using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SemiKick; // этот файл не в namespace SemiKick, а обращается к SemiKickConfig (LegStretch*)


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

    // --- Стретч кости правой ноги ("дотягивание", по аналогии с рукой в самой игре) ---
    // Индекс кости "Bone.004" (правая нога) в boneTransforms/blenderBoneNames.
    // -1, если кость не нашлась при Initialize — тогда стретч просто выключен.
    private int rightLegBoneIndex = -1;
    // Мировая точка, до которой пытаемся "дотянуться" в текущем проигрыше
    // (передаётся снаружи в PlayKick — обычно это hit.point рейкаста пинка).
    // null = стретча в этом проигрыше не будет (промах/цель не найдена).
    private Vector3? stretchTargetWorldPos;
    // Текущее (сглаженное) значение множителя localScale по оси стретча.
    private float currentStretchFactor = 1f;

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

        // Запоминаем индекс кости правой ноги для стретча (см. поле
        // rightLegBoneIndex). Ищем по blender-имени "Bone.004", т.к. это
        // ключ BoneMap, а не unity-имя трансформа.
        rightLegBoneIndex = System.Array.IndexOf(blenderBoneNames, "Bone.004");
        if (rightLegBoneIndex < 0)
        {
            Debug.LogWarning("[JSONAnimation] Кость правой ноги (Bone.004 / 'Player Spring Impulse - Leg Right') " +
                "не найдена в рантайм-иерархии — стретч ноги работать не будет для этого аватара.");
        }

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

        // Сбрасываем стретч ноги к нормальному размеру — иначе после
        // окончания анимации нога так и останется растянутой.
        stretchTargetWorldPos = null;
        currentStretchFactor = 1f;
        if (rightLegBoneIndex >= 0 && rightLegBoneIndex < boneTransforms.Length && boneTransforms[rightLegBoneIndex] != null)
        {
            boneTransforms[rightLegBoneIndex].localScale = Vector3.one;
        }

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

        // Стретч ноги считаем ПОСЛЕ применения ротаций этого кадра — иначе
        // legBone.position ниже была бы мировой позицией с ротацией
        // предыдущего кадра, что для замера дистанции до цели не годится.
        if (rightLegBoneIndex >= 0 && rightLegBoneIndex < boneTransforms.Length && boneTransforms[rightLegBoneIndex] != null)
        {
            ApplyLegStretch(boneTransforms[rightLegBoneIndex]);
        }
    }

    /// <summary>
    /// "Дотягивание" ногой до цели — грубый аналог механики самой игры,
    /// где рука растягивается, если персонаж физически не достаёт до
    /// предмета (см. обсуждение — точный класс/поле в самой игре я не
    /// нашёл и не проверял, поэтому здесь своя независимая реализация,
    /// целиком в этом классе, без завязки на internal-поля игры).
    ///
    /// Логика: если дистанция от текущей мировой позиции кости ноги до
    /// stretchTargetWorldPos больше "естественного дотягивания" —
    /// растягиваем кость по localScale вдоль LegStretchAxis пропорционально
    /// нехватке дистанции, с потолком в LegStretchMaxMultiplier.
    /// ⚠️ Какая именно локальная ось кости "Player Spring Impulse - Leg
    /// Right" соответствует направлению вдоль ноги — НЕ проверено (см.
    /// SemiKickConfig.LegStretchAxis). Если растягивает не в ту сторону —
    /// перебрать 0/1/2.
    /// </summary>
    private void ApplyLegStretch(Transform legBone)
    {
        float targetFactor = 1f;

        if (stretchTargetWorldPos.HasValue)
        {
            float distance = Vector3.Distance(legBone.position, stretchTargetWorldPos.Value);
            float naturalReach = SemiKickConfig.LegStretchNaturalReach.Value;

            if (naturalReach > 0f && distance > naturalReach)
            {
                targetFactor = Mathf.Clamp(distance / naturalReach, 1f, SemiKickConfig.LegStretchMaxMultiplier.Value);
            }
        }

        // Плавно тянемся к целевому множителю, а не телепортируем scale —
        // резкий скачок кости на глаз выглядел бы как баг/рывок.
        currentStretchFactor = Mathf.Lerp(
            currentStretchFactor,
            targetFactor,
            Time.deltaTime * SemiKickConfig.LegStretchLerpSpeed.Value);

        Vector3 scale = Vector3.one;
        switch (SemiKickConfig.LegStretchAxis.Value)
        {
            case 0: scale.x = currentStretchFactor; break;
            case 1: scale.y = currentStretchFactor; break;
            default: scale.z = currentStretchFactor; break;
        }

        legBone.localScale = scale;
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

    /// <summary>
    /// targetWorldPoint — точка (обычно hit.point рейкаста пинка), до которой
    /// нога пытается "дотянуться" стретчем localScale (см. ApplyLegStretch).
    /// null — стретча не будет (например, промах, или вызов из RPC_PlayKick
    /// на чужом клиенте, который не знает точку попадания кикера).
    /// </summary>
    public void PlayKick(Vector3? targetWorldPoint = null)
    {
        Debug.Log($"[JSONAnimation] PlayKick вызван: isReady={isReady}, isPlaying={isPlaying}, gameObject={name}, targetWorldPoint={(targetWorldPoint.HasValue ? targetWorldPoint.Value.ToString() : "NULL")}.");

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

        stretchTargetWorldPos = targetWorldPoint;
        currentStretchFactor = 1f;

        StartCoroutine(RunKick());
    }
}