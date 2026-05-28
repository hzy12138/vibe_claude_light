#!/usr/bin/env python3
"""Detect running Claude Code processes."""
import subprocess
from typing import Callable

class ProcessWatcher:
    def __init__(self, on_process_started: Callable[[int, str], None],
                 on_processExited: Callable[[int], None]):
        self.on_process_started = on_process_started
        self.on_processExited = on_processExited
        self._known_pids: set[int] = set()
        self._running = False
        self._after_id = None

    def start(self, root):
        self._running = True
        self._poll(root)

    def stop(self):
        self._running = False

    def _poll(self, root):
        if not self._running:
            return
        try:
            result = subprocess.run(
                ["tasklist", "/FI", "IMAGENAME eq claude.exe", "/FO", "CSV", "/NH"],
                capture_output=True, text=True, timeout=5
            )
            current_pids = set()
            for line in result.stdout.strip().split("\n"):
                if not line.strip():
                    continue
                parts = line.split(",")
                if len(parts) >= 2:
                    try:
                        pid = int(parts[1].strip('"'))
                        current_pids.add(pid)
                        if pid not in self._known_pids:
                            self._known_pids.add(pid)
                            self.on_process_started(pid, "")
                    except ValueError:
                        pass

            exited = self._known_pids - current_pids
            for pid in exited:
                self._known_pids.discard(pid)
                self.on_processExited(pid)
        except Exception:
            pass
        self._after_id = root.after(3000, lambda: self._poll(root))
