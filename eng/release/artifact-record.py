#!/usr/bin/env python3
"""Bind a release to its original source commit and compare registry package payloads."""
import hashlib
import io
import json
import sys
import tarfile
import urllib.request
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def record_path(directory, version):
    return Path(directory) / f'release-{version}.json'


def package_payload(data):
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        return {name: archive.read(name) for name in archive.namelist()
                if not name.endswith('/') and name != '.signature.p7s'}


def compare_payload(local, published):
    if package_payload(local) != package_payload(published):
        raise ValueError('Published NuGet payload differs from the preserved release artifact')


def validate_metadata(directory, version, commit):
    for suffix in ('', '.Analyzers', '.Incubating'):
        package = Path(directory) / f'Qyl.Telemetry.SemanticConventions{suffix}.{version}.nupkg'
        with zipfile.ZipFile(package) as archive:
            root = ET.fromstring(archive.read(next(n for n in archive.namelist() if n.endswith('.nuspec'))))
        repository = root.find('.//{*}repository')
        if repository is None or repository.get('commit') != commit:
            raise ValueError(f'{package.name}: repository commit differs from {commit}')
        if root.findtext('.//{*}version') != version:
            raise ValueError(f'{package.name}: package version differs from {version}')
    with tarfile.open(Path(directory) / f'ancplua-qyl-semantic-conventions-{version}.tgz') as archive:
        package = json.load(archive.extractfile('package/package.json'))
    if package['version'] != version or package['name'] != '@ancplua/qyl-semantic-conventions':
        raise ValueError('npm artifact name or version differs from the release')


def create(directory, version, commit):
    validate_metadata(directory, version, commit)
    path = record_path(directory, version)
    if path.exists():
        raise ValueError(f'{path} already exists; preserve the original record')
    artifacts = sorted([*Path(directory).glob('*.nupkg'), *Path(directory).glob('*.tgz')])
    if len(artifacts) != 4:
        raise ValueError('Expected exactly four release artifacts')
    path.write_text(json.dumps({'version': version, 'commit': commit,
                               'publication_started': False, 'artifacts': {p.name: digest(p) for p in artifacts}}, indent=2) + '\n')
    print(f'Recorded original artifacts for {version} at {commit}')


def verify(directory, version):
    record = json.loads(record_path(directory, version).read_text())
    if record['version'] != version or len(record['artifacts']) != 4:
        raise ValueError('Invalid release record')
    for name, expected in record['artifacts'].items():
        if Path(name).name != name or digest(Path(directory) / name) != expected:
            raise ValueError(f'{name}: preserved artifact differs from release record')
    validate_metadata(directory, version, record['commit'])
    return record


def mark_started(directory, version):
    record = verify(directory, version)
    record['publication_started'] = True
    record_path(directory, version).write_text(json.dumps(record, indent=2) + '\n')


def verify_registry(directory, version):
    record = verify(directory, version)
    for name in record['artifacts']:
        if not name.endswith('.nupkg'):
            continue
        package_id = name.removesuffix(f'.{version}.nupkg').lower()
        url = f'https://api.nuget.org/v3-flatcontainer/{package_id}/{version}/{package_id}.{version}.nupkg'
        with urllib.request.urlopen(url) as response:
            published = response.read()
        compare_payload((Path(directory) / name).read_bytes(), published)
        print(f'Published payload matches preserved {name} (excluding registry signature only)')


if __name__ == '__main__':
    command, directory, version, *rest = sys.argv[1:]
    if command == 'create':
        create(directory, version, rest[0])
    elif command == 'commit':
        print(verify(directory, version)['commit'])
    elif command == 'mark-started':
        mark_started(directory, version)
    elif command == 'verify-registry':
        verify_registry(directory, version)
    else:
        raise SystemExit('usage: artifact-record.py create|commit|verify-registry directory version [commit]')
