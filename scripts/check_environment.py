"""Read-only diagnostics; no CAD files are opened and no COM app is launched."""
import argparse
import csv
from importlib import metadata
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

try:
    import tomllib
except ImportError:
    tomllib = None

ROOT = Path(__file__).resolve().parents[1]
KEYS = {'SOLIDWORKS_MCP_PATH', 'PYTHON_EXECUTABLE', 'CASES_PATH', 'SOLIDWORKS_MCP_EXECUTABLE', 'SOLIDWORKS_EXE', 'SOLIDWORKS_VERSION', 'CODEX_MCP_CONFIG', 'SW_MCP_OUTPUT_ROOT', 'SW_MCP_TEMPLATE_DIR', 'SMOKE_OUTPUT_ROOT'}


def load_env(path):
    settings = {}
    for number, line in enumerate(Path(path).read_text(encoding='utf-8-sig').splitlines(), 1):
        line = line.strip()
        if not line or line.startswith('#'):
            continue
        key, separator, value = line.partition('=')
        key, value = key.strip(), value.strip()
        if not separator or key not in KEYS or key in settings or '\x00' in value:
            raise ValueError('Invalid, unknown or duplicate setting at line ' + str(number))
        if value[:1] in ('"', "'"):
            if len(value) < 2 or value[-1] != value[0]:
                raise ValueError('Unclosed quote at line ' + str(number))
            value = value[1:-1]
        settings[key] = value
    return settings


def settings_from(path):
    settings = {key: os.environ.get(key, '') for key in KEYS}
    if Path(path).is_file():
        settings.update({k: v for k, v in load_env(path).items() if not settings.get(k)})
    return settings


def local_path(value):
    path = Path(value).expanduser()
    return path if path.is_absolute() else ROOT / path


def executable(value):
    if not value or re.search(r'<[^>]+>', value):
        return None
    path = local_path(value)
    return str(path) if path.is_file() else shutil.which(value)


def installed_path(settings):
    if settings.get('SOLIDWORKS_EXE'):
        path = local_path(settings['SOLIDWORKS_EXE'])
        return path if path.is_file() else None
    if sys.platform != 'win32':
        return None
    import winreg
    key = r'SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\SLDWORKS.exe'
    for hive in (winreg.HKEY_LOCAL_MACHINE, winreg.HKEY_CURRENT_USER):
        for view in (winreg.KEY_WOW64_64KEY, winreg.KEY_WOW64_32KEY):
            try:
                with winreg.OpenKey(hive, key, 0, winreg.KEY_READ | view) as handle:
                    path = Path(winreg.QueryValueEx(handle, None)[0].strip('"'))
                if path.is_file():
                    return path
            except OSError:
                pass
    return None


def running_status():
    if sys.platform != 'win32':
        return False, 'Windows is required for SolidWorks COM'
    try:
        result = subprocess.run(['tasklist', '/FI', 'IMAGENAME eq SLDWORKS.exe', '/FO', 'CSV', '/NH'], capture_output=True, text=True, errors='replace', timeout=10)
        if result.returncode:
            return False, 'Process listing unavailable; check manually'
        running = any(row and row[0].lower() == 'sldworks.exe' for row in csv.reader(result.stdout.splitlines()))
        return running, 'Process found; COM connection not tested' if running else 'Start SolidWorks and close modal dialogs'
    except (OSError, subprocess.TimeoutExpired):
        return False, 'Process listing failed/timed out; check manually'


def check_codex(path):
    if tomllib is None:
        return False, 'Use Python 3.11+ to parse Codex TOML'
    try:
        with Path(path).open('rb') as stream:
            config = tomllib.load(stream)
        servers = config.get('mcp_servers', {})
        entry = servers.get('solidworks') if isinstance(servers, dict) else None
        if not isinstance(entry, dict) or entry.get('enabled', True) is False:
            return False, 'Missing/disabled mcp_servers.solidworks entry'
        command = entry.get('command')
        args = entry.get('args', [])
        if not isinstance(command, str) or not isinstance(args, list) or not all(isinstance(x, str) for x in args):
            return False, 'Invalid command/args'
        if not executable(command) or re.search(r'<[^>]+>', ' '.join(args)):
            return False, 'Replace placeholders with an installed MCP executable'
        env = entry.get('env', {})
        output = env.get('SW_MCP_OUTPUT_ROOT', '') if isinstance(env, dict) else ''
        if not isinstance(output, str) or not output or '<' in output or not local_path(output).is_dir():
            return False, 'Set an existing case output folder as env.SW_MCP_OUTPUT_ROOT'
        return True, 'Entry and paths valid; actual MCP/COM calls still require smoke testing'
    except (OSError, ValueError, TypeError):
        return False, 'Configuration missing, unreadable or invalid TOML'


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--env-file', type=Path, default=ROOT / 'config/solidworks.local.env')
    parser.add_argument('--strict', action='store_true', help='Return 1 when any required item is missing; default always completes with 0')
    args = parser.parse_args(argv)
    missing = []

    def report(label, ok, detail):
        print(('[OK] ' if ok else '[MISSING] ') + label + ' - ' + detail)
        if not ok:
            missing.append(label)

    try:
        settings = settings_from(args.env_file)
    except (OSError, ValueError) as error:
        report('Local configuration', False, str(error))
        settings = {key: os.environ.get(key, '') for key in KEYS}
    report('Python', sys.version_info >= (3, 11), sys.version.split()[0] + '; project tools require 3.11+, recommend 3.12 x64')
    git_found = bool(shutil.which('git'))
    report('Git', git_found, 'Available' if git_found else 'Install Git for Windows')
    report('SolidWorks installed', bool(installed_path(settings)), 'Registry/configured executable check only; set SOLIDWORKS_EXE if detection fails')
    report('SolidWorks running', *running_status())
    checkout = settings['SOLIDWORKS_MCP_PATH']
    source_ok = bool(checkout) and (local_path(checkout) / 'solidworks_mcp/server.py').is_file()
    cli = executable(settings['SOLIDWORKS_MCP_EXECUTABLE'] or 'solidworks-mcp')
    mcp_found = source_ok and bool(cli)
    report('SolidWorks MCP', mcp_found, 'External checkout and CLI found; no connection attempted' if mcp_found else 'Set external Slacker-LLC checkout and installed SOLIDWORKS_MCP_EXECUTABLE')
    for package in ('mcp', 'pywin32', 'jsonschema'):
        try:
            version = metadata.version(package)
            ok = package != 'mcp' or version.split('.')[0] == '1'
            report(package, ok, version + ('; MCP must stay below 2' if package == 'mcp' else ''))
        except metadata.PackageNotFoundError:
            report(package, False, 'Install project requirements into the selected venv')
    for key in ('PYTHON_EXECUTABLE', 'CASES_PATH', 'SW_MCP_OUTPUT_ROOT'):
        value = settings[key]
        ok = bool(executable(value)) if key == 'PYTHON_EXECUTABLE' else bool(value) and local_path(value).is_dir()
        report(key, ok, 'Path found; no CAD files inspected' if ok else 'Set a valid local path')
    config = settings['CODEX_MCP_CONFIG']
    if config:
        path = local_path(config)
    elif (ROOT / '.codex/config.toml').is_file():
        path = ROOT / '.codex/config.toml'
    else:
        path = Path(os.environ.get('CODEX_HOME') or (Path.home() / '.codex')) / 'config.toml'
    report('Codex MCP config', *check_codex(path))
    print('[INFO] No CAD model was opened. License, COM, feature creation and geometry evaluation remain unverified.')
    print('[SUMMARY] Missing items: ' + str(len(missing)))
    return 1 if args.strict and missing else 0


if __name__ == '__main__':
    sys.exit(main())
