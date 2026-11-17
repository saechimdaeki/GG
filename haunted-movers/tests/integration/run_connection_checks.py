"""Run failure recovery and a real two-process ENet day transition. Requires Godot."""
from pathlib import Path
import subprocess
import tempfile
import time

PROJECT = Path(__file__).resolve().parents[2]
COMMAND = ["godot", "--headless", "--path", str(PROJECT), "-s", "res://tests/integration/connection_check.gd"]


def check(label: str, code: int, output: str) -> None:
    print(f"[{label}]\n{output}", flush=True)
    if code != 0 or "PASS connection" not in output or "ERROR:" in output or "FAIL" in output:
        raise RuntimeError(f"{label} failed (exit {code})")


def main() -> None:
    result = subprocess.run(COMMAND, capture_output=True, text=True, timeout=20)
    check("failure/cancel/retry", result.returncode, result.stdout + result.stderr)
    with tempfile.TemporaryDirectory(prefix="haunted-connection-") as folder:
        logs = [Path(folder) / "server.log", Path(folder) / "client.log"]
        processes = []
        try:
            with logs[0].open("w") as server_log, logs[1].open("w") as client_log:
                processes.append(subprocess.Popen(COMMAND + ["--", "--server"], stdout=server_log, stderr=subprocess.STDOUT))
                time.sleep(1)
                processes.append(subprocess.Popen(COMMAND + ["--", "--client"], stdout=client_log, stderr=subprocess.STDOUT))
                for process in processes:
                    process.wait(timeout=20)
            for label, process, log in zip(["server", "client"], processes, logs):
                check(label, process.returncode, log.read_text())
        finally:
            for process in processes:
                if process.poll() is None:
                    process.kill()
                    process.wait()
    print("PASS network integration")


if __name__ == "__main__":
    main()
