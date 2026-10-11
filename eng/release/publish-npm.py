#!/usr/bin/env python3
"""Run Bun once and fail immediately if the registry requests interactive authentication."""
import subprocess
import sys


def run(command):
    with subprocess.Popen(command, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                          stderr=subprocess.STDOUT, text=True, bufsize=1) as process:
        for line in process.stdout:
            lower = line.lower()
            if any(prompt in lower for prompt in ('authenticate your account', 'one-time password', 'enter otp')):
                process.terminate()
                try:
                    process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()
                print('npm requested interactive authentication; publication stopped. Renew the Vitals credential.', file=sys.stderr)
                return 3
            print(line, end='', flush=True)
        return process.wait()


if __name__ == '__main__':
    raise SystemExit(run(sys.argv[1:]))
