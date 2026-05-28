import json
import tempfile
from pathlib import Path
from unittest.mock import patch

def test_install_hooks_creates_config():
    with tempfile.TemporaryDirectory() as tmp:
        settings_path = Path(tmp) / "settings.json"
        settings_path.write_text("{}")

        with patch("hook_installer.get_settings_path", return_value=settings_path):
            with patch("hook_installer.get_cli_path", return_value="/fake/hook_cli.py"):
                import hook_installer
                hook_installer.install_hooks()

        settings = json.loads(settings_path.read_text())
        assert "hooks" in settings
        assert "PreToolUse" in settings["hooks"]
        assert "PostToolUse" in settings["hooks"]
        assert "Notification" in settings["hooks"]

def test_install_hooks_no_duplicate():
    with tempfile.TemporaryDirectory() as tmp:
        settings_path = Path(tmp) / "settings.json"
        settings_path.write_text("{}")

        with patch("hook_installer.get_settings_path", return_value=settings_path):
            with patch("hook_installer.get_cli_path", return_value="/fake/hook_cli.py"):
                import hook_installer
                hook_installer.install_hooks()
                count1 = settings_path.read_text().count("claude-light")
                hook_installer.install_hooks()
                count2 = settings_path.read_text().count("claude-light")

        assert count1 == count2

def test_remove_hooks():
    with tempfile.TemporaryDirectory() as tmp:
        settings_path = Path(tmp) / "settings.json"
        settings_path.write_text('{"hooks": {"PreToolUse": []}}')

        with patch("hook_installer.get_settings_path", return_value=settings_path):
            import hook_installer
            hook_installer.remove_hooks()

        settings = json.loads(settings_path.read_text())
        assert "hooks" not in settings
