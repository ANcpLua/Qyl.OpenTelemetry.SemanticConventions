import importlib.util
import tempfile
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location('artifact_set', Path(__file__).parents[2] / 'eng/release/verify-artifact-set.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class ArtifactSetTests(unittest.TestCase):
    def test_requires_exact_independent_sets(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            names = ['Qyl.Telemetry.SemanticConventions' + s + '.9.9.0.nupkg' for s in ('', '.Incubating', '.Analyzers')]
            names.append('ancplua-qyl-semantic-conventions-9.9.0.tgz')
            for name in names:
                (root / name).touch()
            module.verify(root, '9.9.0')
            for name in names:
                (root / name).unlink()
                with self.assertRaises(ValueError):
                    module.verify(root, '9.9.0')
                (root / name).touch()
            for extra in ('unexpected.nupkg', 'unexpected.tgz'):
                (root / extra).touch()
                with self.assertRaises(ValueError):
                    module.verify(root, '9.9.0')
                (root / extra).unlink()
