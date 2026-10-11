import copy
import importlib.util
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location('typescript_generator', Path(__file__).resolve().parents[2] / 'scripts/generate-typescript.py')
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

class SnapshotTests(unittest.TestCase):
    def test_checkout_location_does_not_change_semantic_snapshot(self):
        first = {'registry': {'attributes': [{'key': 'service.name', 'provenance': {'path': '/tmp/cache-a/model.yaml'}}]}}
        second = copy.deepcopy(first)
        second['registry']['attributes'][0]['provenance']['path'] = '/tmp/cache-b/model.yaml'
        self.assertEqual(generator.semantic_snapshot(first), generator.semantic_snapshot(second))

    def test_nested_registry_semantic_changes_are_preserved(self):
        original = {'dependencies': {'core': {'registry': {'metrics': [{'name': 'duration', 'unit': 's', 'instrument': 'histogram'}]}}}}
        for field, value in [('name', 'latency'), ('unit', 'ms'), ('instrument', 'counter')]:
            changed = copy.deepcopy(original)
            changed['dependencies']['core']['registry']['metrics'][0][field] = value
            self.assertNotEqual(generator.semantic_snapshot(original), generator.semantic_snapshot(changed))

    def test_source_url_changes_are_preserved(self):
        first = {'provenance': {'source': 'https://example.org/schema/1', 'path': 'https://example.org/repo@first/model.yaml'}}
        for field in ('source', 'path'):
            second = copy.deepcopy(first)
            second['provenance'][field] += '/changed'
            self.assertNotEqual(generator.semantic_snapshot(first), generator.semantic_snapshot(second))
