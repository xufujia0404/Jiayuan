#!/usr/bin/env python3
import argparse
import os
import shlex
import subprocess
import sys
from pathlib import Path


DEFAULT_UNIRIG_ROOT = Path("/Users/yixianliu/Project/project/agent_list/UniRig")
LOCAL_OPT_CONFIG_DIR = "pretrained/facebook_opt_350m"
LOCAL_SKELETON_CKPT = "pretrained/VAST-AI_UniRig/skeleton/articulation-xl_quantization_256/model.ckpt"

CPU_MODEL_CONFIG_TEMPLATE = """__target__: unirig_ar
llm:
  pretrained_model_name_or_path: {llm_pretrained_path}
  n_positions: 3076
  max_position_embeddings: 3076
  hidden_size: 1024
  word_embed_proj_dim: 1024
  do_layer_norm_before: True
  _attn_implementation: eager

mesh_encoder:
  __target__: michelangelo_encoder
  pretrained_path: ~
  freeze_encoder: False
  device: cpu
  dtype: float32
  num_latents: 512
  embed_dim: 64
  point_feats: 3
  num_freqs: 8
  include_pi: False
  heads: 8
  width: 512
  num_encoder_layers: 16
  use_ln_post: True
  init_scale: 0.25
  qkv_bias: False
  use_checkpoint: False
  flash: False
  supervision_type: sdf
  query_method: False
  token_num: 1024
"""

CPU_TASK_CONFIG_TEMPLATE = """mode: predict
debug: False
experiment_name: quick_inference_skeleton_articulationxl_ar_256_cpu_local
resume_from_checkpoint: {skeleton_ckpt_path}

components:
  data: quick_inference
  tokenizer: tokenizer_parts_articulationxl_256
  transform: inference_ar_transform
  model: unirig_ar_350m_1024_81920_cpu_local
  system: ar_inference_articulationxl
  data_name: raw_data.npz

writer:
  __target__: ar
  output_dir: ~
  add_num: False
  repeat: 1
  export_npz: predict_skeleton
  export_obj: skeleton
  export_fbx: skeleton

trainer:
  max_epochs: 1
  num_nodes: 1
  devices: 1
  precision: 32
  accelerator: cpu
  strategy: auto
"""

PARSE_PY_PATCH = """from .spec import ModelSpec


def get_model(**kwargs) -> ModelSpec:
    __target__ = kwargs['__target__']
    del kwargs['__target__']

    if __target__ == 'unirig_ar':
        from .unirig_ar import UniRigAR
        return UniRigAR(**kwargs)
    if __target__ == 'unirig_skin':
        from .unirig_skin import UniRigSkin
        return UniRigSkin(**kwargs)

    raise AssertionError(f"expect: [unirig_ar,unirig_skin], found: {__target__}")
"""

PARSE_ENCODER_PY_PATCH = """from dataclasses import dataclass

from .michelangelo.get_model import get_encoder as get_encoder_michelangelo
from .michelangelo.get_model import AlignedShapeLatentPerceiver
from .michelangelo.get_model import get_encoder_simplified as get_encoder_michelangelo_encoder
from .michelangelo.get_model import ShapeAsLatentPerceiverEncoder
try:
    from .pointcept.models.PTv3Object import get_encoder as get_encoder_ptv3obj
    from .pointcept.models.PTv3Object import PointTransformerV3Object
except Exception:
    get_encoder_ptv3obj = None
    PointTransformerV3Object = None


@dataclass(frozen=True)
class _MAP_MESH_ENCODER:
    ptv3obj = PointTransformerV3Object
    michelangelo = AlignedShapeLatentPerceiver
    michelangelo_encoder = ShapeAsLatentPerceiverEncoder


MAP_MESH_ENCODER = _MAP_MESH_ENCODER()


def get_mesh_encoder(**kwargs):
    mapper = {
        'michelangelo': get_encoder_michelangelo,
        'michelangelo_encoder': get_encoder_michelangelo_encoder,
    }
    if get_encoder_ptv3obj is not None:
        mapper['ptv3obj'] = get_encoder_ptv3obj

    __target__ = kwargs['__target__']
    del kwargs['__target__']
    assert __target__ in mapper, f"expect: [{','.join(mapper.keys())}], found: {__target__}"
    return mapper[__target__](**kwargs)
"""


def run_cmd(cmd: list[str], cwd: Path | None = None, env: dict | None = None) -> None:
    print(f"+ {shlex.join(cmd)}")
    subprocess.run(cmd, cwd=str(cwd) if cwd else None, env=env, check=True)


def ensure_text_file(path: Path, content: str) -> None:
    old = path.read_text(encoding="utf-8") if path.exists() else None
    if old == content:
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")
    print(f"[info] updated: {path}")


def ensure_unirig_patches(unirig_root: Path) -> None:
    parse_py = unirig_root / "src/model/parse.py"
    parse_encoder_py = unirig_root / "src/model/parse_encoder.py"

    if parse_py.exists():
        text = parse_py.read_text(encoding="utf-8")
        if "from .unirig_skin import UniRigSkin" in text:
            parse_py.write_text(PARSE_PY_PATCH, encoding="utf-8")
            print(f"[info] patched for skeleton-only import: {parse_py}")

    if parse_encoder_py.exists():
        text = parse_encoder_py.read_text(encoding="utf-8")
        marker = "from .pointcept.models.PTv3Object import get_encoder as get_encoder_ptv3obj"
        if marker in text and "try:" not in text:
            parse_encoder_py.write_text(PARSE_ENCODER_PY_PATCH, encoding="utf-8")
            print(f"[info] patched optional pointcept import: {parse_encoder_py}")


def build_local_or_remote_refs(unirig_root: Path) -> tuple[str, str]:
    local_opt = unirig_root / LOCAL_OPT_CONFIG_DIR
    local_opt_cfg = local_opt / "config.json"
    local_skeleton_ckpt = unirig_root / LOCAL_SKELETON_CKPT

    llm_pretrained_path = str(local_opt) if local_opt_cfg.exists() else "facebook/opt-350m"
    skeleton_ckpt_path = (
        str(local_skeleton_ckpt)
        if local_skeleton_ckpt.exists()
        else "experiments/skeleton/articulation-xl_quantization_256/model.ckpt"
    )

    if llm_pretrained_path == "facebook/opt-350m" or skeleton_ckpt_path.startswith("experiments/"):
        print(
            "[warn] local pretrained assets not fully found under UniRig/pretrained. "
            "pipeline may access HuggingFace."
        )
    else:
        print("[info] using local pretrained assets under UniRig/pretrained")

    return llm_pretrained_path, skeleton_ckpt_path


def ensure_cpu_configs(unirig_root: Path) -> tuple[Path, Path]:
    llm_pretrained_path, skeleton_ckpt_path = build_local_or_remote_refs(unirig_root)
    model_cfg = unirig_root / "configs/model/unirig_ar_350m_1024_81920_cpu_local.yaml"
    task_cfg = unirig_root / "configs/task/quick_inference_skeleton_articulationxl_ar_256_cpu_local.yaml"
    model_content = CPU_MODEL_CONFIG_TEMPLATE.format(llm_pretrained_path=llm_pretrained_path)
    task_content = CPU_TASK_CONFIG_TEMPLATE.format(skeleton_ckpt_path=skeleton_ckpt_path)
    ensure_text_file(model_cfg, model_content)
    ensure_text_file(task_cfg, task_content)
    return model_cfg, task_cfg


def resolve_python_bin(unirig_root: Path, python_bin_arg: str | None) -> Path:
    if python_bin_arg:
        python_bin = Path(python_bin_arg).expanduser()
        if not python_bin.is_absolute():
            python_bin = (Path.cwd() / python_bin)
    else:
        default_python = unirig_root / ".venv311/bin/python"
        python_bin = default_python if default_python.exists() else Path(sys.executable)

    if not python_bin.exists():
        raise FileNotFoundError(f"python binary not found: {python_bin}")
    return python_bin


def derive_auto_humanoid_named_path(stem: str, output_dir: Path) -> Path:
    base = output_dir / f"{stem}_unirig_generate_skeleton_humanoid_named.fbx"
    if not base.exists():
        return base

    version = 2
    while True:
        candidate = output_dir / f"{stem}_unirig_generate_skeleton_humanoid_named_v{version}.fbx"
        if not candidate.exists():
            return candidate
        version += 1


def check_modules(python_bin: Path, modules: list[str]) -> list[str]:
    missing = []
    for mod in modules:
        ret = subprocess.run(
            [str(python_bin), "-c", f"import {mod}"],
            capture_output=True,
            text=True,
        )
        if ret.returncode != 0:
            missing.append(mod)
    return missing


def auto_install_deps(python_bin: Path) -> None:
    packages = [
        "numpy==1.26.4",
        "scipy",
        "pyyaml",
        "python-box",
        "tqdm",
        "trimesh",
        "fast-simplification",
        "open3d",
        "bpy",
        "torch",
        "torchvision",
        "transformers==4.51.3",
        "lightning",
        "pytorch_lightning",
        "omegaconf",
        "einops",
        "timm",
        "huggingface_hub",
        "pyrender",
        "wandb",
    ]
    run_cmd([str(python_bin), "-m", "pip", "install"] + packages)
    run_cmd([str(python_bin), "-m", "pip", "install", "--no-build-isolation", "torch-cluster"])


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input-fbx", required=True, help="input fbx absolute path")
    parser.add_argument("--output-dir", default=None, help="output dir, default=input dir")
    parser.add_argument("--unirig-root", default=str(DEFAULT_UNIRIG_ROOT), help="UniRig project root")
    parser.add_argument("--python-bin", default=None, help="python used for UniRig")
    parser.add_argument("--seed", type=int, default=12345)
    parser.add_argument("--auto-install", action="store_true", help="auto install missing deps")
    parser.add_argument(
        "--require-local-assets",
        action="store_true",
        help="require local assets under UniRig/pretrained, fail if missing",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    input_fbx = Path(args.input_fbx).expanduser().resolve()
    if not input_fbx.exists():
        raise FileNotFoundError(f"input fbx not found: {input_fbx}")
    if input_fbx.suffix.lower() != ".fbx":
        raise ValueError(f"input must be .fbx: {input_fbx}")

    output_dir = Path(args.output_dir).expanduser().resolve() if args.output_dir else input_fbx.parent
    output_dir.mkdir(parents=True, exist_ok=True)

    unirig_root = Path(args.unirig_root).expanduser().resolve()
    if not unirig_root.exists():
        raise FileNotFoundError(f"unirig root not found: {unirig_root}")

    python_bin = resolve_python_bin(unirig_root, args.python_bin)

    ensure_unirig_patches(unirig_root)
    _, task_cfg = ensure_cpu_configs(unirig_root)

    if args.require_local_assets:
        local_opt_cfg = unirig_root / LOCAL_OPT_CONFIG_DIR / "config.json"
        local_skeleton_ckpt = unirig_root / LOCAL_SKELETON_CKPT
        missing_local = [str(p) for p in [local_opt_cfg, local_skeleton_ckpt] if not p.exists()]
        if missing_local:
            raise RuntimeError(
                "required local assets missing under UniRig/pretrained: "
                + ", ".join(missing_local)
            )

    required = ["bpy", "torch", "lightning", "transformers", "torch_cluster"]
    missing = check_modules(python_bin, required)
    if missing:
        if args.auto_install:
            print(f"[warn] missing deps: {missing}, start auto install ...")
            auto_install_deps(python_bin)
        else:
            raise RuntimeError(
                "missing python deps: "
                + ",".join(missing)
                + f". run with --auto-install or install into {python_bin.parent.parent}"
            )

    stem = input_fbx.stem
    generated = output_dir / f"{stem}_unirig_generate_skeleton.fbx"
    renamed = derive_auto_humanoid_named_path(stem, output_dir)
    rename_script = Path(__file__).resolve().parent / "rename_unirig_skeleton_to_humanoid.py"

    run_cmd(
        [
            "bash",
            "launch/inference/generate_skeleton.sh",
            "--input",
            str(input_fbx),
            "--output",
            str(generated),
            "--skeleton_task",
            str(task_cfg),
            "--seed",
            str(args.seed),
        ],
        cwd=unirig_root,
        env={
            **os.environ,
            "PATH": str(python_bin.parent) + os.pathsep + os.environ.get("PATH", ""),
        },
    )

    if not generated.exists():
        raise RuntimeError(f"generate_skeleton output missing: {generated}")

    run_cmd(
        [
            str(python_bin),
            str(rename_script),
            "--input",
            str(generated),
            "--output",
            str(renamed),
        ]
    )

    if not renamed.exists():
        raise RuntimeError(f"humanoid renamed output missing: {renamed}")

    print("[info] humanoid rename strategy: semantic+topology (default)")
    print("[info] next step for animation-ready rig: run run_unirig_humanoid_skin_pipeline.py")
    print("\n[done] outputs:")
    print(f"- skeleton: {generated}")
    print(f"- humanoid_named: {renamed}")


if __name__ == "__main__":
    main()
