import importlib.util
import io
import json
import subprocess
import sys
import tarfile
import tempfile
import unittest
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('artifact_record', ROOT / 'eng/release/artifact-record.py')
record = importlib.util.module_from_spec(spec)
spec.loader.exec_module(record)


def archive(entries):
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, 'w') as package:
        for name, value in entries.items():
            package.writestr(name, value)
    return buffer.getvalue()


class ResumeTests(unittest.TestCase):
    def test_only_registry_signature_is_ignored(self):
        local = archive({'library.dll': b'original', 'package.nuspec': b'metadata'})
        record.compare_payload(local, archive({'library.dll': b'original', 'package.nuspec': b'metadata', '.signature.p7s': b'signature'}))
        for entries in ({'library.dll': b'changed', 'package.nuspec': b'metadata'},
                        {'library.dll': b'original', 'package.nuspec': b'metadata', 'extra.dll': b'extra'}):
            with self.assertRaises(ValueError):
                record.compare_payload(local, archive(entries))

    def test_record_binds_all_four_artifacts_and_the_source_commit(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for suffix in ('', '.Analyzers', '.Incubating'):
                (root / f'Qyl.Telemetry.SemanticConventions{suffix}.9.9.0.nupkg').write_bytes(archive({
                    'package.nuspec': '<package><metadata><version>9.9.0</version><repository commit="original" /></metadata></package>',
                    'library.dll': b'original'}))
            path = root / 'ancplua-qyl-semantic-conventions-9.9.0.tgz'
            data = json.dumps({'name': '@ancplua/qyl-semantic-conventions', 'version': '9.9.0'}).encode()
            with tarfile.open(path, 'w:gz') as package:
                info = tarfile.TarInfo('package/package.json')
                info.size = len(data)
                package.addfile(info, io.BytesIO(data))
            record.create(root, '9.9.0', 'original')
            self.assertEqual('original', record.verify(root, '9.9.0')['commit'])
            with self.assertRaises(ValueError):
                record.create(root, '9.9.0', 'changed')
            record.mark_started(root, '9.9.0')
            self.assertTrue(record.verify(root, '9.9.0')['publication_started'])
            path.write_bytes(b'changed')
            with self.assertRaises(ValueError):
                record.verify(root, '9.9.0')

    def test_interactive_npm_auth_stops_without_retry_or_waiting(self):
        code = 'import time; print("Authenticate your account at", flush=True); time.sleep(60)'
        result = subprocess.run([sys.executable, str(ROOT / 'eng/release/publish-npm.py'), sys.executable, '-c', code], capture_output=True, text=True, timeout=10)
        self.assertEqual(3, result.returncode)
        self.assertIn('publication stopped', result.stderr)
        self.assertNotIn('Authenticate your account at', result.stdout)

    def test_browser_auth_requires_explicit_opt_in(self):
        code = 'print("Authenticate your account at https://example.invalid/auth", flush=True)'
        result = subprocess.run([sys.executable, str(ROOT / 'eng/release/publish-npm.py'), '--browser-auth', sys.executable, '-c', code], capture_output=True, text=True, timeout=10)
        self.assertEqual(0, result.returncode)
        self.assertIn('https://example.invalid/auth', result.stdout)
