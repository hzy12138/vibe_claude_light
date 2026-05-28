#!/usr/bin/env python3
"""Auto-install hooks into Claude Code settings.json."""
import json
import sys
from pathlib import Path

HOOK_MARKER = "claude-light"

def get_settings_path() -> Path:
    return Path.home() / ".claude" / "settings.json"

def get_cli_path() -> str:
    return str(Path(__file__).parent / "hook_cli.py")

def install_hooks():
    settings_path = get_settings_path()
    cli_path = get_cli_path()
    python_exe = sys.executable

    settings = {}
    if settings_path.exists():
        settings = json.loads(settings_path.read_text(encoding="utf-8"))

    hooks = settings.get("hooks", {})
    if any(HOOK_MARKER in json.dumps(v) for v in hooks.values()):
        print("Hooks already installed.")
        return

    settings["hooks"] = {
        "PreToolUse": [{
            "hooks": [{
                "type": "command",
                "command": f'"{python_exe}" "{cli_path}" hook running $CLAUDE_PROJECT_DIR'
            }]
        }],
        "PostToolUse": [{
            "hooks": [{
                "type": "command",
                "command": f'"{python_exe}" "{cli_path}" hook done $CLAUDE_PROJECT_DIR'
            }]
        }],
        "Notification": [{
            "hooks": [{
                "type": "command",
                "command": f'"{python_exe}" "{cli_path}" hook confirm $CLAUDE_PROJECT_DIR'
            }]
        }]
    }

    settings_path.parent.mkdir(parents=True, exist_ok=True)
    settings_path.write_text(json.dumps(settings, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"Hooks installed to {settings_path}")

def remove_hooks():
    settings_path = get_settings_path()
    if not settings_path.exists():
        return

    settings = json.loads(settings_path.read_text(encoding="utf-8"))
    if "hooks" in settings:
        del settings["hooks"]
        settings_path.write_text(json.dumps(settings, indent=2, ensure_ascii=False), encoding="utf-8")
        print(f"Removed hooks from {settings_path}")

if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "remove":
        remove_hooks()
    else:
        install_hooks()
