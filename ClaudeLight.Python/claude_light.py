#!/usr/bin/env python3
"""Claude Light — Traffic light indicator for Claude Code (Python/tkinter version)."""
import sys
import tkinter as tk
from pathlib import Path
from typing import Optional

try:
    import ctypes
    user32 = ctypes.windll.user32
    HAS_CTYPES = True
except AttributeError:
    HAS_CTYPES = False

from state_manager import StateManager
from process_watcher import ProcessWatcher

COLORS = {
    "off": "#333333",
    "red": "#e74c3c",
    "yellow": "#f39c12",
    "green": "#2ecc71",
}

GWL_EXSTYLE = -20
WS_EX_LAYERED = 0x80000
WS_EX_TRANSPARENT = 0x20


class TrafficLightWindow(tk.Toplevel):
    def __init__(self, project_dir: str, master=None):
        super().__init__(master)
        self.project_dir = project_dir
        self.project_name = Path(project_dir).name
        self.is_horizontal = False
        self._blink_on = False
        self._blink_id = None

        self.title("Claude Light")
        self.overrideredirect(False)
        self.attributes("-topmost", True)
        self.attributes("-alpha", 0.85)
        self.configure(bg="#1e1e1e", highlightbackground="#555", highlightthickness=1)

        screen_w = self.winfo_screenwidth()
        self.geometry(f"100x200+{screen_w - 120}+100")

        self._build_ui()
        self._setup_click_through()
        self._setup_drag()

    def _build_ui(self):
        self.frame = tk.Frame(self, bg="#1e1e1e")
        self.frame.pack(fill=tk.BOTH, expand=True, padx=8, pady=8)

        self.vert_frame = tk.Frame(self.frame, bg="#1e1e1e")
        self.vert_frame.pack()

        self.red_v = tk.Canvas(self.vert_frame, width=30, height=30, bg=COLORS["off"], highlightthickness=0)
        self.red_v.pack(pady=(0, 4))
        self.yellow_v = tk.Canvas(self.vert_frame, width=30, height=30, bg=COLORS["off"], highlightthickness=0)
        self.yellow_v.pack(pady=(0, 4))
        self.green_v = tk.Canvas(self.vert_frame, width=30, height=30, bg=COLORS["off"], highlightthickness=0)
        self.green_v.pack()

        tk.Frame(self.frame, height=1, bg="#555").pack(fill=tk.X, pady=6)
        tk.Label(self.frame, text=self.project_name, fg="#aaa", bg="#1e1e1e",
                 font=("Segoe UI", 8)).pack()
        tk.Label(self.frame, text=self.project_dir, fg="#666", bg="#1e1e1e",
                 font=("Segoe UI", 7), wraplength=90).pack()

        for canvas in [self.red_v, self.yellow_v, self.green_v]:
            canvas.create_oval(2, 2, 28, 28, fill=COLORS["off"], outline="", tags="light")

    def _setup_click_through(self):
        if not HAS_CTYPES:
            return
        hwnd = user32.GetParent(self.winfo_id())
        style = user32.GetWindowLongW(hwnd, GWL_EXSTYLE)
        user32.SetWindowLongW(hwnd, GWL_EXSTYLE, style | WS_EX_LAYERED | WS_EX_TRANSPARENT)

    def _setup_drag(self):
        self._drag_data = {"x": 0, "y": 0}
        self.bind("<ButtonPress-1>", self._start_drag)
        self.bind("<B1-Motion>", self._do_drag)
        self.bind("<Double-Button-1>", self._toggle_layout)

    def _start_drag(self, event):
        self._drag_data["x"] = event.x
        self._drag_data["y"] = event.y

    def _do_drag(self, event):
        dx = event.x - self._drag_data["x"]
        dy = event.y - self._drag_data["y"]
        x = self.winfo_x() + dx
        y = self.winfo_y() + dy
        self.geometry(f"+{x}+{y}")

    def _toggle_layout(self, event):
        self.is_horizontal = not self.is_horizontal
        if self.is_horizontal:
            self.geometry("140x120")
        else:
            self.geometry("100x200")

    def set_status(self, status: Optional[str]):
        if self._blink_id:
            self.after_cancel(self._blink_id)
            self._blink_id = None

        colors = {c: COLORS["off"] for c in ["red", "yellow", "green"]}

        if status == "running":
            colors["red"] = COLORS["red"]
        elif status == "confirm":
            colors["yellow"] = COLORS["yellow"]
            self._blink()
        elif status == "done":
            colors["green"] = COLORS["green"]

        self.red_v.itemconfig("light", fill=colors["red"])
        self.yellow_v.itemconfig("light", fill=colors["yellow"])
        self.green_v.itemconfig("light", fill=colors["green"])

        if status is None:
            self.withdraw()
        else:
            self.deiconify()

    def _blink(self):
        self._blink_on = not self._blink_on
        color = COLORS["yellow"] if self._blink_on else COLORS["off"]
        self.yellow_v.itemconfig("light", fill=color)
        self._blink_id = self.after(500, self._blink)

    def cleanup(self):
        if self._blink_id:
            self.after_cancel(self._blink_id)


class ClaudeLightApp:
    def __init__(self):
        self.root = tk.Tk()
        self.root.withdraw()

        self.windows: dict[str, TrafficLightWindow] = {}
        self.state_manager = StateManager(self._on_status_change)
        self.process_watcher = ProcessWatcher(self._on_process_started, self._on_process_exited)

        self.root.bind("<Button-3>", self._show_menu)

    def _show_menu(self, event):
        menu = tk.Menu(self.root, tearoff=0)
        menu.add_command(label="Show/Hide All", command=self._toggle_visibility)
        menu.add_separator()
        menu.add_command(label="Exit", command=self._exit)
        menu.post(event.x_root, event.y_root)

    def _toggle_visibility(self):
        for w in self.windows.values():
            if w.state() == "normal":
                w.withdraw()
            else:
                w.deiconify()

    def _on_status_change(self, project_dir: str, status: Optional[str]):
        if status is None:
            if project_dir in self.windows:
                self.windows[project_dir].cleanup()
                self.windows[project_dir].destroy()
                del self.windows[project_dir]
        else:
            if project_dir not in self.windows:
                self.windows[project_dir] = TrafficLightWindow(project_dir, self.root)
            self.windows[project_dir].set_status(status)

    def _on_process_started(self, pid: int, project_dir: str):
        pass

    def _on_process_exited(self, pid: int):
        pass

    def _exit(self):
        self.state_manager.stop()
        self.process_watcher.stop()
        for w in self.windows.values():
            w.cleanup()
        self.root.destroy()

    def run(self):
        from hook_installer import install_hooks
        install_hooks()

        self.state_manager.start(self.root)
        self.process_watcher.start(self.root)
        self.root.mainloop()


def main():
    app = ClaudeLightApp()
    app.run()


if __name__ == "__main__":
    main()
