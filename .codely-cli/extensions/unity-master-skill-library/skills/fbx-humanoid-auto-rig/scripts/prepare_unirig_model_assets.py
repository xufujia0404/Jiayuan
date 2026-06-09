#!/usr/bin/env python3
import argparse
import json
from pathlib import Path

from huggingface_hub import hf_hub_download
from transformers import AutoConfig


DEFAULT_UNIRIG_ROOT = Path("/Users/yixianliu/Project/project/agent_list/UniRig")
OPT_REPO_ID = "facebook/opt-350m"
UNIRIG_REPO_ID = "VAST-AI/UniRig"

SKELETON_CKPT = "skeleton/articulation-xl_quantization_256/model.ckpt"
SKIN_CKPTS = [
    "skin/articulation-xl/model.ckpt",
    "skin/skeleton/model.ckpt",
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--unirig-root", default=str(DEFAULT_UNIRIG_ROOT), help="UniRig project root")
    parser.add_argument("--force-download", action="store_true", help="force redownload even if local file exists")
    parser.add_argument("--skip-skin-ckpt", action="store_true", help="only download skeleton checkpoint")
    return parser.parse_args()


def ensure_parent(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)


def ensure_opt_config(unirig_root: Path, force_download: bool) -> Path:
    opt_dir = unirig_root / "pretrained/facebook_opt_350m"
    cfg_file = opt_dir / "config.json"
    if cfg_file.exists() and not force_download:
        return cfg_file

    opt_dir.mkdir(parents=True, exist_ok=True)
    cfg = AutoConfig.from_pretrained(OPT_REPO_ID)
    cfg.save_pretrained(str(opt_dir))
    return cfg_file


def ensure_unirig_ckpt(unirig_root: Path, filename: str, force_download: bool) -> Path:
    repo_local_dir = unirig_root / "pretrained/VAST-AI_UniRig"
    target = repo_local_dir / filename
    if target.exists() and not force_download:
        return target

    ensure_parent(target)
    hf_hub_download(
        repo_id=UNIRIG_REPO_ID,
        filename=filename,
        local_dir=str(repo_local_dir),
        local_dir_use_symlinks=False,
        force_download=force_download,
    )
    return target


def main() -> None:
    args = parse_args()
    unirig_root = Path(args.unirig_root).expanduser().resolve()
    if not unirig_root.exists():
        raise FileNotFoundError(f"unirig root not found: {unirig_root}")

    report = {
        "unirig_root": str(unirig_root),
        "opt_config": "",
        "skeleton_ckpt": "",
        "skin_ckpts": [],
    }

    opt_cfg = ensure_opt_config(unirig_root, args.force_download)
    skeleton_ckpt = ensure_unirig_ckpt(unirig_root, SKELETON_CKPT, args.force_download)
    report["opt_config"] = str(opt_cfg)
    report["skeleton_ckpt"] = str(skeleton_ckpt)

    if not args.skip_skin_ckpt:
        for ckpt in SKIN_CKPTS:
            p = ensure_unirig_ckpt(unirig_root, ckpt, args.force_download)
            report["skin_ckpts"].append(str(p))

    report_path = unirig_root / "pretrained/prepare_report.json"
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

    print("[done] local model assets prepared")
    print(f"- opt_config: {report['opt_config']}")
    print(f"- skeleton_ckpt: {report['skeleton_ckpt']}")
    if report["skin_ckpts"]:
        for item in report["skin_ckpts"]:
            print(f"- skin_ckpt: {item}")
    print(f"- report: {report_path}")


if __name__ == "__main__":
    main()
