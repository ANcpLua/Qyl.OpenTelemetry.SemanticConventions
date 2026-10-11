#!/usr/bin/env python3
"""Run Bun once and fail immediately if the registry requests interactive authentication."""
import subprocess
import sys


def run(command, allow_browser_auth=False):
    with subprocess.Popen(command, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                          stderr=subprocess.STDOUT, text=True, bufsize=1) as process:
        for line in process.stdout:
            lower = line.lower()
            if (('authenticate your account' in lower and not allow_browser_auth)
                    or any(prompt in lower for prompt in ('one-time password', 'enter otp'))):
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
    args = sys.argv[1:]
    browser_auth = bool(args and args[0] == '--browser-auth')
    raise SystemExit(run(args[1:] if browser_auth else args, browser_auth))
