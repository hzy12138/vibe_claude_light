#!/usr/bin/env python3
"""Claude Light cleanup — remove all hooks and data."""
import sys
import shutil
from pathlib import Path

def main():
    force = "--force" in sys.argv

    if not force:
        answer = input("This will remove all Claude Light hooks and data. Continue? (y/N): ")
        if answer.lower() != "y":
            print("Cancelled.")
            return 0

    from hook_installer import remove_hooks
    remove_hooks()

    state_dir = Path.home() / ".claude-lights"
    if state_dir.exists():
        shutil.rmtree(state_dir)
        print(f"Deleted {state_dir}")

    cli_dir = Path.home() / ".claude-light"
    if cli_dir.exists():
        shutil.rmtree(cli_dir)
        print(f"Deleted {cli_dir}")

    print("Claude Light has been uninstalled.")
    return 0

if __name__ == "__main__":
    sys.exit(main())
