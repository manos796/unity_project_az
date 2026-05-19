#!/usr/bin/env python3
"""
Synchronous Unity Bridge command wrapper for macOS/Linux.

Writes a JSON command to Assets/LLM/Bridge/request.json and waits for
Assets/LLM/Bridge/response.md to update.
"""

from __future__ import annotations

import argparse
import sys
import time
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Send a Unity Bridge command and wait for the response."
    )
    parser.add_argument(
        "command",
        nargs="?",
        help='JSON command string, e.g. \'{"type": "help"}\'',
    )
    parser.add_argument(
        "-f",
        "--file",
        dest="file_path",
        help="Path to a JSON file containing the command.",
    )
    parser.add_argument(
        "--timeout",
        type=float,
        default=60.0,
        help="Timeout in seconds while waiting for Unity Bridge response.",
    )
    args = parser.parse_args()

    if args.file_path and args.command:
        parser.error("Specify either a command string or --file, not both.")
    if not args.file_path and not args.command:
        parser.error("Provide a command string or --file.")

    return args


def load_command(args: argparse.Namespace) -> str:
    if args.file_path:
        file_path = Path(args.file_path)
        if not file_path.is_file():
            raise FileNotFoundError(f"File not found: {file_path}")
        command = file_path.read_text(encoding="utf-8")
        if not command.strip():
            raise ValueError(f"File is empty: {file_path}")
        return command

    command = args.command or ""
    if not command.strip():
        raise ValueError("Command is empty.")
    return command


def main() -> int:
    try:
        args = parse_args()
        command = load_command(args)
    except Exception as exc:
        print(str(exc), file=sys.stderr)
        return 1

    project_root = Path(__file__).resolve().parent
    bridge_folder = project_root / "Assets" / "LLM" / "Bridge"
    request_file = bridge_folder / "request.json"
    response_file = bridge_folder / "response.md"

    bridge_folder.mkdir(parents=True, exist_ok=True)

    before_mtime_ns = (
        response_file.stat().st_mtime_ns if response_file.exists() else -1
    )

    request_file.write_text(command, encoding="utf-8")

    deadline = time.monotonic() + args.timeout
    poll_interval = 0.5

    while time.monotonic() < deadline:
        time.sleep(poll_interval)

        if not response_file.exists():
            continue

        current_mtime_ns = response_file.stat().st_mtime_ns
        if current_mtime_ns <= before_mtime_ns:
            continue

        sys.stdout.write(response_file.read_text(encoding="utf-8"))
        return 0

    print(
        f"Timeout ({args.timeout:g} s) waiting for Unity response. Is Unity running?",
        file=sys.stderr,
    )
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
