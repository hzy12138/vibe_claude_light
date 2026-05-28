#!/usr/bin/env python3
"""Claude Light hook CLI — called by Claude Code hooks to write state files."""
import sys
import os
import json
import time
from pathlib import Path

def get_state_dir():
    return Path.home() / ".claude-lights"

def get_state_file(project_dir: str) -> Path:
    sanitized = project_dir.replace("\\", "_").replace("/", "_").replace(":", "_")
    if len(sanitized) > 100:
        sanitized = sanitized[-100:]
    return get_state_dir() / f"{sanitized}.json"

def main():
    if len(sys.argv) < 4 or sys.argv[1] != "hook":
        print("Usage: python hook_cli.py hook <running|confirm|done|exit> <project_dir>", file=sys.stderr)
        return 1

    action = sys.argv[2]
    project_dir = sys.argv[3]
    state_dir = get_state_dir()
    state_dir.mkdir(parents=True, exist_ok=True)

    if action == "exit":
        state_file = get_state_file(project_dir)
        if state_file.exists():
            state_file.unlink()
        return 0

    status_map = {"running": "running", "confirm": "confirm", "done": "done"}
    status = status_map.get(action)
    if not status:
        print(f"Unknown action: {action}", file=sys.stderr)
        return 1

    project_name = Path(project_dir).name
    state = {
        "project_name": project_name,
        "project_dir": project_dir,
        "status": status,
        "pid": os.getpid(),
        "updated_at": time.strftime("%Y-%m-%dT%H:%M:%S", time.gmtime()),
    }

    state_file = get_state_file(project_dir)
    state_file.write_text(json.dumps(state, indent=2))
    return 0

if __name__ == "__main__":
    sys.exit(main())
