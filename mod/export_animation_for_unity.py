"""
Экспорт поворотов костей анимации в JSON, покадрово.
Запускать в Blender: Scripting workspace -> вставить -> Run Script (Alt+P)

Как использовать:
1. Выделите объект Armature с активным Action (вашей анимацией пинка)
2. Проверьте/поправьте ARMATURE_NAME ниже
3. Запустите скрипт — он выгрузит JSON рядом с .blend файлом
4. Откройте JSON, посмотрите список имён костей — дальше вручную сопоставите
   их с путями в игре (см. следующий шаг с C#)
"""

import bpy
import json
import os

ARMATURE_NAME = "Armature"     # имя объекта арматуры в Outliner
OUTPUT_FILENAME = "kick_animation.json"

def blender_quat_to_unity(q):
    """
    Конвертирует кватернион поворота из системы координат Blender
    (Z-up, правосторонняя) в систему координат Unity (Y-up, левосторонняя).

    Выведено через явную замену базиса (swap Y<->Z) и подстановку матриц
    поворота вокруг X/Y/Z по отдельности, а не подобрано перебором.
    Формула не зависит от значений — работает для любого поворота.

    Возвращает кортеж (x, y, z, w) уже в системе координат Unity.
    """
    return (-q.x, -q.z, -q.y, q.w)


def export_pose_animation():
    obj = bpy.data.objects.get(ARMATURE_NAME)
    if obj is None or obj.type != 'ARMATURE':
        raise RuntimeError(f"Не найден объект Armature с именем '{ARMATURE_NAME}'")

    action = obj.animation_data.action if obj.animation_data else None
    if action is None:
        raise RuntimeError("У арматуры нет активного Action (анимации)")

    frame_start = int(action.frame_range[0])
    frame_end = int(action.frame_range[1])

    scene = bpy.context.scene
    original_frame = scene.frame_current

    frames_data = []

    for frame in range(frame_start, frame_end + 1):
        scene.frame_set(frame)

        bones_data = {}
        for pbone in obj.pose.bones:
            # локальный поворот кости относительно родителя (рест-позы), в виде кватерниона
            q = pbone.rotation_quaternion if pbone.rotation_mode == 'QUATERNION' \
                else pbone.matrix_basis.to_quaternion()

            # Конвертация системы координат Blender (Z-up, правосторонняя)
            # в Unity (Y-up, левосторонняя): swap Y/Z + смена знака.
            # Выведено через матрицы поворота (X/Y/Z), не подобрано эмпирически.
            qx, qy, qz, qw = blender_quat_to_unity(q)

            bones_data[pbone.name] = {
                "w": round(qw, 6),
                "x": round(qx, 6),
                "y": round(qy, 6),
                "z": round(qz, 6),
            }

        frames_data.append({
            "frame": frame,
            "time": (frame - frame_start) / scene.render.fps,
            "bones": bones_data,
        })

    scene.frame_set(original_frame)

    result = {
        "armature": ARMATURE_NAME,
        "fps": scene.render.fps,
        "frame_start": frame_start,
        "frame_end": frame_end,
        "duration": (frame_end - frame_start) / scene.render.fps,
        "bone_names": [pbone.name for pbone in obj.pose.bones],
        "frames": frames_data,
    }

    blend_dir = os.path.dirname(bpy.data.filepath) or os.path.expanduser("~")
    output_path = os.path.join(blend_dir, OUTPUT_FILENAME)

    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(result, f, indent=2)

    print(f"Готово! Экспортировано {len(frames_data)} кадров, {len(result['bone_names'])} костей.")
    print(f"Файл: {output_path}")
    print(f"Кости: {result['bone_names']}")

    return output_path


if __name__ == "__main__":
    export_pose_animation()
