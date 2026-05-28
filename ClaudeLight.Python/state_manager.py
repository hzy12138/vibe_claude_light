#!/usr/bin/env python3
"""Watch ~/.claude-lights/ for state file changes."""
import json
import time
from pathlib import Path
from typing import Callable, Optional

class StateManager:
    def __init__(self, on_status_change: Callable[[str, Optional[str]], None]):
        self.state_dir = Path.home() / ".claude-lights"
        self.state_dir.mkdir(parents=True, exist_ok=True)
        self.on_status_change = on_status_change
        self._file_mtimes: dict[str, float] = {}
        self._running = False
        self._after_id = None

    def start(self, root):
        self._running = True
        self.scan_existing()
        self._poll(root)

    def stop(self):
        self._running = False

    def scan_existing(self):
        for f in self.state_dir.glob("*.json"):
            self._handle_file(f)

    def _poll(self, root):
        if not self._running:
            return
        try:
            current_files = {f.name: f.stat().st_mtime for f in self.state_dir.glob("*.json")}

            for name, mtime in current_files.items():
                if name not in self._file_mtimes or self._file_mtimes[name] != mtime:
                    self._handle_file(self.state_dir / name)

            for name in list(self._file_mtimes.keys()):
                if name not in current_files:
                    self._handle_deletion(name)

            self._file_mtimes = current_files
        except Exception:
            pass
        self._after_id = root.after(1000, lambda: self._poll(root))

    def _handle_file(self, path: Path):
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
            status = data.get("status")
            project_dir = data.get("project_dir", "")
            if project_dir:
                self.on_status_change(project_dir, status)
        except Exception:
            pass

    def _handle_deletion(self, filename: str):
        project_dir = filename.replace(".json", "").replace("_", "/")
        self.on_status_change(project_dir, None)
