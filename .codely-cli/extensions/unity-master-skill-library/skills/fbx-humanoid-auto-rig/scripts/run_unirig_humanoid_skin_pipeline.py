#!/usr/bin/env python3
import argparse
import os
import shlex
import subprocess
import sys
from pathlib import Path


DEFAULT_UNIRIG_ROOT = Path("/Users/yixianliu/Project/project/agent_list/UniRig")


def run_cmd(cmd: list[str], cwd: Path | None = None, env: dict | None = None, check: bool = True) -> int:
    print(f"+ {shlex.join(cmd)}")
    result = subprocess.run(cmd, cwd=str(cwd) if cwd else None, env=env)
    if check and result.returncode != 0:
        raise RuntimeError(f"command failed ({result.returncode}): {shlex.join(cmd)}")
    return result.returncode


def is_valid_file(path: Path) -> bool:
    return path.exists() and path.is_file() and path.stat().st_size > 0


def derive_skin_output_path(input_humanoid_fbx: Path, output_dir: Path) -> Path:
    stem = input_humanoid_fbx.stem
    if "_unirig_generate_skeleton_humanoid_named_" in stem:
        stem = stem.replace("_unirig_generate_skeleton_humanoid_named_", "_unirig_generate_skin_humanoid_named_")
    elif "_unirig_generate_skeleton" in stem:
        stem = stem.replace("_unirig_generate_skeleton", "_unirig_generate_skin")
    else:
        stem = stem + "_unirig_generate_skin"
    return output_dir / f"{stem}.fbx"


def run_unirig_generate_skin(
    unirig_root: Path,
    python_bin: Path,
    input_humanoid_fbx: Path,
    output_skin_fbx: Path,
    seed: int,
) -> bool:
    env = os.environ.copy()
    env["PATH"] = str(python_bin.parent) + os.pathsep + env.get("PATH", "")

    cmd = [
        "bash",
        "launch/inference/generate_skin.sh",
        "--input",
        str(input_humanoid_fbx),
        "--output",
        str(output_skin_fbx),
        "--seed",
        str(seed),
    ]
    # generate_skin.sh may still return 0 even when inner python jobs fail.
    run_cmd(cmd, cwd=unirig_root, env=env, check=False)
    return is_valid_file(output_skin_fbx)


def run_blender_autoweight_fallback(
    python_bin: Path,
    input_humanoid_fbx: Path,
    output_skin_fbx: Path,
) -> None:
    script = f"""
import bpy, os
input_fbx = r\"{str(input_humanoid_fbx)}\"
output_fbx = r\"{str(output_skin_fbx)}\"

for coll in [bpy.data.actions,bpy.data.armatures,bpy.data.cameras,bpy.data.collections,bpy.data.images,bpy.data.materials,bpy.data.meshes,bpy.data.objects,bpy.data.textures]:
    for obj in list(coll):
        try:
            coll.remove(obj)
        except Exception:
            pass

bpy.ops.import_scene.fbx(filepath=input_fbx, use_image_search=False, ignore_leaf_bones=False)
armatures=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
if not armatures:
    raise RuntimeError('No armature found in input')
if not meshes:
    raise RuntimeError('No mesh found in input')

arm=armatures[0]
for m in meshes:
    for mod in list(m.modifiers):
        if mod.type=='ARMATURE':
            m.modifiers.remove(mod)
    m.parent = None

bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True)
for m in meshes:
    m.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.parent_set(type='ARMATURE_AUTO')

os.makedirs(os.path.dirname(output_fbx), exist_ok=True)
bpy.ops.export_scene.fbx(filepath=output_fbx, use_selection=False, apply_unit_scale=True, bake_space_transform=False, add_leaf_bones=False, path_mode='COPY', embed_textures=False)
print('[autoweight] output', output_fbx)
""".strip()

    run_cmd([str(python_bin), "-c", script], check=True)

    if not is_valid_file(output_skin_fbx):
        raise RuntimeError(f"autoweight fallback failed, output missing: {output_skin_fbx}")


def run_merge(
    unirig_root: Path,
    python_bin: Path,
    source_skin_fbx: Path,
    merge_target: Path,
    merge_output: Path,
) -> None:
    env = os.environ.copy()
    env["PATH"] = str(python_bin.parent) + os.pathsep + env.get("PATH", "")
    run_cmd(
        [
            "bash",
            "launch/inference/merge.sh",
            "--source",
            str(source_skin_fbx),
            "--target",
            str(merge_target),
            "--output",
            str(merge_output),
        ],
        cwd=unirig_root,
        env=env,
        check=True,
    )
    if not is_valid_file(merge_output):
        raise RuntimeError(f"merge output missing: {merge_output}")


def resolve_python_bin(unirig_root: Path, python_bin_arg: str | None) -> Path:
    if python_bin_arg:
        python_bin = Path(python_bin_arg).expanduser()
        if not python_bin.is_absolute():
            python_bin = Path.cwd() / python_bin
    else:
        default_python = unirig_root / ".venv311/bin/python"
        python_bin = default_python if default_python.exists() else Path(sys.executable)

    if not python_bin.exists():
        raise FileNotFoundError(f"python binary not found: {python_bin}")
    return python_bin


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input-humanoid-fbx", required=True, help="input humanoid fbx absolute path")
    parser.add_argument("--output-dir", default=None, help="output dir, default=input dir")
    parser.add_argument("--unirig-root", default=str(DEFAULT_UNIRIG_ROOT), help="UniRig project root")
    parser.add_argument("--python-bin", default=None, help="python used for UniRig/Blender bpy")
    parser.add_argument("--seed", type=int, default=12345)
    parser.add_argument("--disable-autoweight-fallback", action="store_true")
    parser.add_argument(
        "--force-autoweight",
        action="store_true",
        help="skip UniRig generate_skin and directly run Blender Armature Auto Weights",
    )
    parser.add_argument("--merge-target", default=None, help="optional original model for merge, e.g. xxx.glb")
    parser.add_argument("--merge-output", default=None, help="optional merge output path")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    input_humanoid_fbx = Path(args.input_humanoid_fbx).expanduser().resolve()
    if not input_humanoid_fbx.exists():
        raise FileNotFoundError(f"input humanoid fbx not found: {input_humanoid_fbx}")
    if input_humanoid_fbx.suffix.lower() != ".fbx":
        raise ValueError(f"input must be .fbx: {input_humanoid_fbx}")

    output_dir = Path(args.output_dir).expanduser().resolve() if args.output_dir else input_humanoid_fbx.parent
    output_dir.mkdir(parents=True, exist_ok=True)

    unirig_root = Path(args.unirig_root).expanduser().resolve()
    if not unirig_root.exists():
        raise FileNotFoundError(f"unirig root not found: {unirig_root}")

    python_bin = resolve_python_bin(unirig_root, args.python_bin)

    output_skin_fbx = derive_skin_output_path(input_humanoid_fbx, output_dir)

    if args.force_autoweight:
        print("[info] skin strategy: Blender Auto Weights (forced)")
        run_blender_autoweight_fallback(
            python_bin=python_bin,
            input_humanoid_fbx=input_humanoid_fbx,
            output_skin_fbx=output_skin_fbx,
        )
        print("[info] Blender Auto Weights completed")
    else:
        print("[info] step: generate skin with UniRig")
        ok = run_unirig_generate_skin(
            unirig_root=unirig_root,
            python_bin=python_bin,
            input_humanoid_fbx=input_humanoid_fbx,
            output_skin_fbx=output_skin_fbx,
            seed=args.seed,
        )

        if not ok:
            if args.disable_autoweight_fallback:
                raise RuntimeError(
                    "generate_skin output missing and autoweight fallback is disabled: "
                    + str(output_skin_fbx)
                )
            print("[warn] UniRig generate_skin failed or output missing, fallback to Blender Auto Weights")
            run_blender_autoweight_fallback(
                python_bin=python_bin,
                input_humanoid_fbx=input_humanoid_fbx,
                output_skin_fbx=output_skin_fbx,
            )
            print("[info] fallback autoweight completed")
        else:
            print("[info] UniRig generate_skin completed")

    merge_output = None
    if args.merge_target:
        merge_target = Path(args.merge_target).expanduser().resolve()
        if not merge_target.exists():
            raise FileNotFoundError(f"merge target not found: {merge_target}")
        if args.merge_output:
            merge_output = Path(args.merge_output).expanduser().resolve()
        else:
            merge_output = output_dir / f"{input_humanoid_fbx.stem}_rigged.glb"
        merge_output.parent.mkdir(parents=True, exist_ok=True)
        print("[info] step: merge skin result to target model")
        run_merge(
            unirig_root=unirig_root,
            python_bin=python_bin,
            source_skin_fbx=output_skin_fbx,
            merge_target=merge_target,
            merge_output=merge_output,
        )

    print("\n[done] outputs:")
    print(f"- skin_fbx: {output_skin_fbx}")
    if merge_output is not None:
        print(f"- merged_model: {merge_output}")


if __name__ == "__main__":
    main()
