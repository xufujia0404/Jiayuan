#!/usr/bin/env python3
import argparse
import os
import re
from collections import defaultdict
from math import sqrt

import bpy


REQUIRED = [
    "Hips",
    "Spine",
    "Chest",
    "UpperChest",
    "Neck",
    "Head",
    "LeftShoulder",
    "LeftUpperArm",
    "LeftLowerArm",
    "LeftHand",
    "RightShoulder",
    "RightUpperArm",
    "RightLowerArm",
    "RightHand",
    "LeftUpperLeg",
    "LeftLowerLeg",
    "LeftFoot",
    "LeftToes",
    "RightUpperLeg",
    "RightLowerLeg",
    "RightFoot",
    "RightToes",
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, help="input FBX absolute path")
    parser.add_argument("--output", required=True, help="output FBX absolute path")
    return parser.parse_args()


def clear_scene() -> None:
    for coll in [
        bpy.data.actions,
        bpy.data.armatures,
        bpy.data.cameras,
        bpy.data.collections,
        bpy.data.images,
        bpy.data.materials,
        bpy.data.meshes,
        bpy.data.objects,
        bpy.data.textures,
    ]:
        for obj in list(coll):
            try:
                coll.remove(obj)
            except Exception:
                pass


def dist(a, b) -> float:
    dx = a.x - b.x
    dy = a.y - b.y
    dz = a.z - b.z
    return sqrt(dx * dx + dy * dy + dz * dz)


def child_map(edit_bones):
    result = defaultdict(list)
    for bone in edit_bones:
        if bone.parent is not None:
            result[bone.parent.name].append(bone)
    return result


def descendants_count(name: str, children: dict, cache: dict) -> int:
    if name in cache:
        return cache[name]
    total = 0
    for child in children.get(name, []):
        total += 1 + descendants_count(child.name, children, cache)
    cache[name] = total
    return total


def chain_longest(children: dict, start, max_nodes: int):
    chain = []
    current = start
    for _ in range(max_nodes):
        if current is None:
            break
        chain.append(current)
        next_children = children.get(current.name, [])
        if not next_children:
            break
        current = max(next_children, key=lambda b: dist(current.head, b.head))
    return chain


def pick_spine_root(hips, children: dict):
    candidates = children.get(hips.name, [])
    if not candidates:
        return None
    return max(candidates, key=lambda b: b.head.z)


def pick_leg_roots(hips, children: dict, spine_root):
    candidates = [b for b in children.get(hips.name, []) if spine_root is None or b.name != spine_root.name]
    if len(candidates) < 2:
        return None, None
    # NOTE:
    # Blender FBX export + Unity FBX import uses different handedness conversion.
    # In our pipeline this leads to X-side inversion when viewed in Unity.
    # To make final Unity Humanoid side names correct, we intentionally select
    # Left as max(x) and Right as min(x) in Blender space.
    left = max(candidates, key=lambda b: b.head.x)
    right = min(candidates, key=lambda b: b.head.x)
    return left, right


def pick_shoulder(upper_chest, children: dict, neck):
    candidates = []
    for child in children.get(upper_chest.name, []):
        if neck is not None and child.name == neck.name:
            continue
        candidates.append(child)
    if len(candidates) < 2:
        return None, None
    # See note in pick_leg_roots(): choose Blender-space side reversed so that
    # final Unity imported model has correct Left/Right mapping.
    left = max(candidates, key=lambda b: b.head.x)
    right = min(candidates, key=lambda b: b.head.x)
    return left, right


def pick_arm_chain(shoulder, children: dict):
    if shoulder is None:
        return None, None, None
    upper_children = children.get(shoulder.name, [])
    if not upper_children:
        return None, None, None
    upper_arm = max(upper_children, key=lambda b: abs(b.head.x - shoulder.head.x))

    lower_children = children.get(upper_arm.name, [])
    lower_arm = max(lower_children, key=lambda b: dist(upper_arm.head, b.head)) if lower_children else None

    hand_children = children.get(lower_arm.name, []) if lower_arm is not None else []
    hand = max(hand_children, key=lambda b: dist(lower_arm.head, b.head)) if hand_children else None
    return upper_arm, lower_arm, hand


def pick_leg_chain(upper_leg, children: dict):
    if upper_leg is None:
        return None, None, None
    chain = chain_longest(children, upper_leg, 4)
    lower = chain[1] if len(chain) > 1 else None
    foot = chain[2] if len(chain) > 2 else None
    toes = chain[3] if len(chain) > 3 else None
    return lower, foot, toes


def pick_neck_head(upper_chest, children: dict):
    neck = None
    head = None
    candidates = children.get(upper_chest.name, [])
    if candidates:
        neck = max(candidates, key=lambda b: b.head.z)
    if neck is not None:
        neck_children = children.get(neck.name, [])
        if neck_children:
            head = max(neck_children, key=lambda b: b.head.z)
    return neck, head


def pick_finger_branches(hand, children: dict):
    if hand is None:
        return []
    roots = children.get(hand.name, [])
    branches = []
    for root in roots:
        branch = [root]
        current = root
        for _ in range(2):
            next_children = children.get(current.name, [])
            if not next_children:
                break
            current = max(next_children, key=lambda b: dist(branch[-1].head, b.head))
            branch.append(current)
        branches.append(branch)
    return branches


def assign_fingers(branches):
    if not branches:
        return {}
    if len(branches) == 1:
        return {"Middle": branches[0]}

    thumb = max(branches, key=lambda b: b[0].head.z)
    rest = [b for b in branches if b is not thumb]
    rest.sort(key=lambda b: b[0].head.y)
    names = ["Index", "Middle", "Ring", "Little"]

    result = {"Thumb": thumb}
    for idx, branch in enumerate(rest):
        key = names[idx] if idx < len(names) else f"ExtraFinger{idx}"
        result[key] = branch
    return result


def add_mapping(mapping: dict, bone, target_name: str):
    if bone is None or not target_name:
        return
    if bone.name in mapping:
        return
    mapping[bone.name] = target_name


def ensure_optional_head_bones(edit_bones) -> None:
    head = edit_bones.get("Head")
    if head is None:
        return

    cx, cy, cz = head.head.x, head.head.y, head.head.z
    tx, ty, tz = head.tail.x, head.tail.y, head.tail.z
    z_eye = cz + (tz - cz) * 0.65
    y_eye = cy + (ty - cy) * 0.10
    eye_len = max((tz - cz) * 0.12, 0.008)
    x_off = max(abs(head.tail.x - head.head.x), 0.02)

    def ensure_eye(name: str, x_sign: float) -> None:
        bone = edit_bones.get(name) or edit_bones.new(name)
        bone.head = (cx + x_sign * x_off, y_eye, z_eye)
        bone.tail = (cx + x_sign * x_off, y_eye + eye_len, z_eye)
        bone.parent = head
        bone.use_connect = False

    ensure_eye("LeftEye", +1.0)
    ensure_eye("RightEye", -1.0)

    jaw = edit_bones.get("Jaw") or edit_bones.new("Jaw")
    jaw.head = (cx, cy, cz + (tz - cz) * 0.15)
    jaw.tail = (cx, cy - eye_len * 1.5, cz + (tz - cz) * 0.05)
    jaw.parent = head
    jaw.use_connect = False


def apply_rename(edit_bones, mapping: dict):
    all_map = dict(mapping)

    # Rename remaining bone_xx to ExtraBone_xx to remove ambiguous raw names.
    extra = {}
    for bone in list(edit_bones):
        name = bone.name
        if name in all_map:
            continue
        if re.fullmatch(r"bone_\d+", name):
            extra[name] = "ExtraBone_" + name.split("_", 1)[1]
    all_map.update(extra)

    for old_name, new_name in all_map.items():
        bone = edit_bones.get(old_name)
        if bone is not None:
            bone.name = "__tmp__" + new_name

    for new_name in all_map.values():
        bone = edit_bones.get("__tmp__" + new_name)
        if bone is not None:
            bone.name = new_name

    return all_map


def reparent_humanoid(edit_bones) -> None:
    parent_map = {
        "Spine": "Hips",
        "Chest": "Spine",
        "UpperChest": "Chest",
        "Neck": "UpperChest",
        "Head": "Neck",
        "LeftShoulder": "UpperChest",
        "LeftUpperArm": "LeftShoulder",
        "LeftLowerArm": "LeftUpperArm",
        "LeftHand": "LeftLowerArm",
        "RightShoulder": "UpperChest",
        "RightUpperArm": "RightShoulder",
        "RightLowerArm": "RightUpperArm",
        "RightHand": "RightLowerArm",
        "LeftUpperLeg": "Hips",
        "LeftLowerLeg": "LeftUpperLeg",
        "LeftFoot": "LeftLowerLeg",
        "LeftToes": "LeftFoot",
        "RightUpperLeg": "Hips",
        "RightLowerLeg": "RightUpperLeg",
        "RightFoot": "RightLowerLeg",
        "RightToes": "RightFoot",
        "LeftEye": "Head",
        "RightEye": "Head",
        "Jaw": "Head",
    }

    finger_defs = ["Thumb", "Index", "Middle", "Ring", "Little"]
    for side in ["Left", "Right"]:
        hand = f"{side}Hand"
        for finger in finger_defs:
            p = f"{side}{finger}Proximal"
            i = f"{side}{finger}Intermediate"
            d = f"{side}{finger}Distal"
            parent_map[p] = hand
            parent_map[i] = p
            parent_map[d] = i

    for child_name, parent_name in parent_map.items():
        child = edit_bones.get(child_name)
        parent = edit_bones.get(parent_name)
        if child is None or parent is None:
            continue
        child.parent = parent
        child.use_connect = False


def rename_vertex_groups(rename_map: dict) -> None:
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        for old_name, new_name in rename_map.items():
            vg = obj.vertex_groups.get(old_name)
            if vg is not None:
                vg.name = new_name


def main() -> None:
    args = parse_args()
    input_path = os.path.abspath(args.input)
    output_path = os.path.abspath(args.output)
    if not os.path.exists(input_path):
        raise FileNotFoundError(f"input not found: {input_path}")

    clear_scene()
    bpy.ops.import_scene.fbx(filepath=input_path, ignore_leaf_bones=False, use_image_search=False)

    armatures = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
    if not armatures:
        raise RuntimeError("no armature found in input fbx")
    arm_obj = armatures[0]

    bpy.context.view_layer.objects.active = arm_obj
    arm_obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    edit_bones = arm_obj.data.edit_bones
    children = child_map(edit_bones)

    roots = [b for b in edit_bones if b.parent is None]
    if not roots:
        raise RuntimeError("no root bone found")
    cache = {}
    hips = max(roots, key=lambda b: descendants_count(b.name, children, cache))

    spine_root = pick_spine_root(hips, children)
    spine_chain = chain_longest(children, spine_root, 3) if spine_root is not None else []
    spine = spine_chain[0] if len(spine_chain) > 0 else None
    chest = spine_chain[1] if len(spine_chain) > 1 else spine
    upper_chest = spine_chain[2] if len(spine_chain) > 2 else chest

    neck, head = pick_neck_head(upper_chest, children) if upper_chest is not None else (None, None)
    left_shoulder, right_shoulder = pick_shoulder(upper_chest, children, neck) if upper_chest is not None else (None, None)
    left_upper_arm, left_lower_arm, left_hand = pick_arm_chain(left_shoulder, children)
    right_upper_arm, right_lower_arm, right_hand = pick_arm_chain(right_shoulder, children)

    left_upper_leg, right_upper_leg = pick_leg_roots(hips, children, spine_root)
    left_lower_leg, left_foot, left_toes = pick_leg_chain(left_upper_leg, children)
    right_lower_leg, right_foot, right_toes = pick_leg_chain(right_upper_leg, children)

    mapping = {}
    add_mapping(mapping, hips, "Hips")
    add_mapping(mapping, spine, "Spine")
    add_mapping(mapping, chest, "Chest")
    add_mapping(mapping, upper_chest, "UpperChest")
    add_mapping(mapping, neck, "Neck")
    add_mapping(mapping, head, "Head")

    add_mapping(mapping, left_shoulder, "LeftShoulder")
    add_mapping(mapping, left_upper_arm, "LeftUpperArm")
    add_mapping(mapping, left_lower_arm, "LeftLowerArm")
    add_mapping(mapping, left_hand, "LeftHand")
    add_mapping(mapping, right_shoulder, "RightShoulder")
    add_mapping(mapping, right_upper_arm, "RightUpperArm")
    add_mapping(mapping, right_lower_arm, "RightLowerArm")
    add_mapping(mapping, right_hand, "RightHand")

    add_mapping(mapping, left_upper_leg, "LeftUpperLeg")
    add_mapping(mapping, left_lower_leg, "LeftLowerLeg")
    add_mapping(mapping, left_foot, "LeftFoot")
    add_mapping(mapping, left_toes, "LeftToes")
    add_mapping(mapping, right_upper_leg, "RightUpperLeg")
    add_mapping(mapping, right_lower_leg, "RightLowerLeg")
    add_mapping(mapping, right_foot, "RightFoot")
    add_mapping(mapping, right_toes, "RightToes")

    left_branches = assign_fingers(pick_finger_branches(left_hand, children))
    right_branches = assign_fingers(pick_finger_branches(right_hand, children))
    for side, branch_map in [("Left", left_branches), ("Right", right_branches)]:
        for finger_name, branch in branch_map.items():
            if not branch:
                continue
            prox = branch[0] if len(branch) > 0 else None
            inter = branch[1] if len(branch) > 1 else None
            dista = branch[2] if len(branch) > 2 else None
            add_mapping(mapping, prox, f"{side}{finger_name}Proximal")
            add_mapping(mapping, inter, f"{side}{finger_name}Intermediate")
            add_mapping(mapping, dista, f"{side}{finger_name}Distal")

    rename_map = apply_rename(edit_bones, mapping)
    ensure_optional_head_bones(edit_bones)
    reparent_humanoid(edit_bones)

    remaining_bone_xx = [b.name for b in edit_bones if re.fullmatch(r"bone_\\d+", b.name)]
    bpy.ops.object.mode_set(mode="OBJECT")
    arm_obj.select_set(False)

    rename_vertex_groups(rename_map)

    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=output_path,
        use_selection=False,
        apply_unit_scale=True,
        bake_space_transform=False,
        add_leaf_bones=False,
        path_mode="COPY",
        embed_textures=False,
    )

    all_bone_names = [b.name for b in arm_obj.data.bones]
    missing = [name for name in REQUIRED if name not in all_bone_names]
    print("[semantic_rename] input:", input_path)
    print("[semantic_rename] output:", output_path)
    print("[semantic_rename] renamed_count:", len(rename_map))
    print("[semantic_rename] missing_required:", missing)
    print("[semantic_rename] remaining_bone_xx:", remaining_bone_xx)


if __name__ == "__main__":
    main()
