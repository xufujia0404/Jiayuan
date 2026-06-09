#!/usr/bin/env python3
"""
Backward-compatible entrypoint.

Default strategy is semantic+topology humanoid rename, implemented in
rename_unirig_skeleton_semantic_to_humanoid.py.
"""

from rename_unirig_skeleton_semantic_to_humanoid import main


if __name__ == "__main__":
    main()
