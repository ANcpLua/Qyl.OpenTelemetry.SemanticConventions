#!/usr/bin/env python3
"""Require the three NuGet packages and one npm tarball independently."""
import re
import sys
from pathlib import Path


def verify(directory, version):
    if not re.fullmatch(r'\d+\.\d+\.\d+', version):
        raise ValueError('version must be X.Y.Z')
    directory = Path(directory)
    expected_nuget = {f'Qyl.Telemetry.SemanticConventions{suffix}.{version}.nupkg' for suffix in ('', '.Incubating', '.Analyzers')}
    expected_npm = {f'ancplua-qyl-semantic-conventions-{version}.tgz'}
    for pattern, expected in [('*.nupkg', expected_nuget), ('*.tgz', expected_npm)]:
        actual = {p.name for p in directory.glob(pattern) if p.is_file()}
        if actual != expected:
            raise ValueError(f'{pattern}: expected {sorted(expected)}, found {sorted(actual)}')
    print('Verified exactly three expected NuGet packages and one expected npm tarball')


if __name__ == '__main__':
    verify(*sys.argv[1:])
