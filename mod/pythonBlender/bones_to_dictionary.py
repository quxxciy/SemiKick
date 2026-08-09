import bpy

THRESHOLD = 0.0001

def analyze_bones_for_csharp():
    obj = bpy.context.active_object

    if not obj or obj.type != 'ARMATURE':
        print("\n❌ Ошибка: выдели объект Armature во viewport!")
        return

    if not obj.animation_data or not obj.animation_data.action:
        print("\n❌ Ошибка: у арматуры нет активной анимации (Action)!")
        return

    action = obj.animation_data.action
    frame_start = int(action.frame_range[0])
    frame_end = int(action.frame_range[1])
    
    scene = bpy.context.scene
    original_frame = scene.frame_current

    # 1. Поиск движущихся костей
    scene.frame_set(frame_start)
    base_transforms = {pbone.name: pbone.matrix_basis.copy() for pbone in obj.pose.bones}
    moving_bones = set()

    for frame in range(frame_start + 1, frame_end + 1):
        scene.frame_set(frame)
        for pbone in obj.pose.bones:
            if pbone.name in moving_bones:
                continue
            diff = pbone.matrix_basis - base_transforms[pbone.name]
            if sum(abs(val) for col in diff for val in col) > THRESHOLD:
                moving_bones.add(pbone.name)

    scene.frame_set(original_frame)

    # 2. Безопасное выделение костей (совместимо со всеми версиями Blender)
    try:
        bpy.ops.object.mode_set(mode='POSE')
        for pbone in obj.pose.bones:
            try: pbone.bone.select = False
            except: pass
            try: pbone.select = False
            except: pass

        for bone_name in moving_bones:
            if bone_name in obj.pose.bones:
                pb = obj.pose.bones[bone_name]
                try: pb.bone.select = True
                except: pass
                try: pb.select = True
                except: pass
    except Exception:
        pass

    # 3. Генерируем заготовку для C# Dictionary
    sorted_bones = sorted(list(moving_bones))
    
    lines = []
    lines.append(f"//📌 НАЙДЕНО ДВИЖУЩИХСЯ КОСТЕЙ: {len(sorted_bones)} шт.\n")
    lines.append("private static readonly Dictionary<string, string> BoneMap = new Dictionary<string, string>")
    lines.append("{")

    for bone_name in sorted_bones:
        pbone = obj.pose.bones[bone_name]
        head_pos = obj.matrix_world @ pbone.head
        
        height = head_pos.z
        side = head_pos.x
        
        if side > 0.05: side_str = "СПРАВА"
        elif side < -0.05: side_str = "СЛЕВА"
        else: side_str = "ЦЕНТР"

        lines.append(f'    {{ "{bone_name}", "ПУТЬ_К_ОБЪЕКТУ_В_UNITY" }}, // Высота Z={height:.2f} | {side_str}')

    lines.append("};")
    
    lines.append("\n" + "="*50)
    lines.append("ПОДРОБНОСТИ (ИЕРАРХИЯ КОСТЕЙ):")
    lines.append("="*50)

    for i, bone_name in enumerate(sorted_bones, 1):
        pbone = obj.pose.bones[bone_name]
        parent_name = pbone.parent.name if pbone.parent else "Корневая (Root)"
        head_pos = obj.matrix_world @ pbone.head
        lines.append(f'{i}. "{bone_name}"')
        lines.append(f'   ├─ Родитель: {parent_name}')
        lines.append(f'   └─ Позиция (Z - высота, X - право/лево): Z={head_pos.z:.2f}, X={head_pos.x:.2f}\n')

    result_text = "\n".join(lines)

    # Сохранение в файл CSharp_BoneMap.txt внутри Blender
    text_block = bpy.data.texts.get("CSharp_BoneMap.txt") or bpy.data.texts.new("CSharp_BoneMap.txt")
    text_block.clear()
    text_block.write(result_text)

    print("\n" + "="*50)
    print("УСПЕШНО! Результат записан в файл 'CSharp_BoneMap.txt'")
    print("="*50 + "\n")

if __name__ == "__main__":
    analyze_bones_for_csharp()