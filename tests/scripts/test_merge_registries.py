"""Unit tests for scripts/merge_registries.py: the qyl guard and the third source."""
from __future__ import annotations

import hashlib
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.join(
    os.path.dirname(__file__), "..", "..",
    "src", "Qyl.Telemetry.SemanticConventions.SourceGeneration", "scripts"))

from merge_registries import MergeError, merge  # noqa: E402


def source(name, commit):
    return {
        "source_registry": name,
        "schema_url": f"https://opentelemetry.io/schemas/{name}",
        "source_ref": f"ref-{name}",
        "source_commit": commit,
        "source_date_epoch": "1",
    }


def attribute(key, registry):
    return {"key": key, "type": "string", "brief": "b", "note": "", "stability": "stable", **source(registry, "c")}


def metric(name, registry):
    return {
        "metric_name": name, "instrument": "counter", "unit": "1", "metric_requirement_level": "recommended",
        "brief": "b", "note": "", "stability": "stable", "attribute_refs": [], "entity_associations": [],
        "attributes": [], **source(registry, "c"),
    }


def group(group_id, group_type, registry):
    return {"id": group_id, "type": group_type, "brief": "", "note": "", "prefix": "", "attribute_refs": [],
            "attributes": [], **source(registry, "c")}


def projection(registry, catalog=(), metrics=(), groups=()):
    return {
        "schema_url": f"https://opentelemetry.io/schemas/{registry}",
        "semconv_commit": "c",
        "sources": [source(registry, "c")],
        "groups": list(groups),
        "catalog": list(catalog),
        "metrics": list(metrics),
        "events": [],
        "entities": [],
    }


class MergeRegistriesTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.core_model = Path(self.tmp.name) / "core"
        self.genai_model = Path(self.tmp.name) / "genai"
        for model in (self.core_model, self.genai_model):
            model.mkdir()
            (model / "manifest.yaml").write_text("schema_url: https://opentelemetry.io/schemas/x\n")
        self.core = projection("core", catalog=[attribute("http.route", "core")], metrics=[metric("http.server.request.duration", "core")],
                               groups=[group("metric.http.server.request.duration", "metric", "core")])
        self.genai = projection("genai", catalog=[attribute("gen_ai.operation.name", "genai")])

    def tearDown(self):
        self.tmp.cleanup()

    def run_merge(self, qyl):
        """Merge `qyl` against the two-row upstream fixture.

        `qyl.attribute.namespace` is a closed value set the merge recomputes, so every
        fixture carries one that matches whatever the merged catalog ends up holding —
        except the tests that deliberately break it.
        """
        qyl = dict(qyl)
        if "attributes" not in qyl or not any(
                attribute["key"] == "qyl.attribute.namespace" for attribute in qyl.get("attributes", [])):
            qyl["attributes"] = list(qyl.get("attributes", [])) + [self.namespace_attribute(qyl)]
        qyl_bytes = json.dumps(qyl).encode("utf-8")
        return merge(self.core, self.genai, qyl, qyl_bytes, self.core_model, self.genai_model, "1.44.0", "0.25.1"), qyl_bytes

    def namespace_attribute(self, qyl, extra=(), drop=()):
        keys = (
            [row["key"] for row in self.core["catalog"]]
            + [row["key"] for row in self.genai["catalog"]]
            + [row["key"] for row in qyl.get("attributes", [])]
            + [attribute["key"] for model in qyl.get("vendor_models", []) for attribute in model.get("attributes", [])]
            + ["qyl.attribute.namespace"]
        )
        roots = ({key.split(".", 1)[0] for key in keys} | {"other"} | set(extra)) - set(drop)
        return {
            "key": "qyl.attribute.namespace",
            "type": {"members": [
                {"id": root, "value": root, "stability": "development", "brief": f"The {root} namespace."}
                for root in sorted(roots)
            ]},
            "stability": "development",
            "brief": "First dot-segment of an attribute key.",
        }

    def test_qyl_is_the_third_source_and_every_qyl_row_carries_its_provenance(self):
        qyl = {
            "attributes": [{"key": "qyl.project.id", "type": "string", "stability": "development", "brief": "b"}],
            "metrics": [{"metric_name": "nservicebus.messaging.operation.duration", "instrument": "histogram", "unit": "s",
                         "stability": "development", "brief": "b", "attributes": ["http.route"]}],
            "scope_names": ["Qyl.Collector"],
            "event_names": ["qyl.http.client"],
        }
        merged, qyl_bytes = self.run_merge(qyl)

        expected_commit = hashlib.sha256(qyl_bytes).hexdigest()
        self.assertEqual([s["source_registry"] for s in merged["sources"]], ["core", "genai", "qyl"])
        qyl_source = merged["sources"][2]
        self.assertEqual(qyl_source, {
            "source_registry": "qyl", "schema_url": None, "source_ref": "qyl-registry.json",
            "source_commit": expected_commit, "source_date_epoch": None,
        })

        qyl_rows = [row for section in ("catalog", "metrics", "groups") for row in merged[section]
                    if row.get("source_registry") == "qyl"]
        self.assertEqual(len(qyl_rows), 4)  # the attribute, the namespace enum, the metric, its group
        for row in qyl_rows:
            self.assertEqual(row["source_ref"], "qyl-registry.json")
            self.assertEqual(row["source_commit"], expected_commit)
            self.assertIsNone(row["schema_url"])
            self.assertIsNone(row["source_date_epoch"])
        nested = merged["metrics"][-1]["attributes"][0]
        self.assertEqual(nested["key"], "http.route")
        self.assertEqual(nested["source_registry"], "qyl")
        self.assertEqual(nested["source_commit"], expected_commit)

        self.assertEqual([row["key"] for row in merged["catalog"]],
                         ["http.route", "gen_ai.operation.name", "qyl.project.id", "qyl.attribute.namespace"])
        self.assertEqual(merged["scope_names"], ["Qyl.Collector"])
        self.assertEqual(merged["event_names"], ["qyl.http.client"])

    def test_upstream_rows_keep_their_upstream_provenance(self):
        merged, _ = self.run_merge({"attributes": []})
        self.assertEqual(merged["catalog"][0]["source_registry"], "core")
        self.assertEqual(merged["catalog"][0]["schema_url"], "https://opentelemetry.io/schemas/core")
        self.assertEqual(merged["catalog"][1]["source_registry"], "genai")

    def test_attribute_outside_qyl_namespace_is_refused(self):
        with self.assertRaises(MergeError) as raised:
            self.run_merge({"attributes": [{"key": "http.route", "type": "string", "stability": "stable", "brief": "b"}]})
        self.assertEqual(
            str(raised.exception),
            "qyl-registry.json attribute 'http.route' is outside the qyl.* namespace; qyl-owned attributes must "
            "start with 'qyl.', and a third-party key belongs in a declared vendor_models entry")

    def test_attribute_shadowing_an_upstream_key_is_refused(self):
        self.core["catalog"].append(attribute("qyl.taken", "core"))
        with self.assertRaises(MergeError) as raised:
            self.run_merge({"attributes": [{"key": "qyl.taken", "type": "string", "stability": "stable", "brief": "b"}]})
        self.assertEqual(
            str(raised.exception),
            "qyl-registry.json attribute 'qyl.taken' shadows the upstream core catalog row of the same key")

    def test_metric_shadowing_an_upstream_metric_is_refused(self):
        with self.assertRaises(MergeError) as raised:
            self.run_merge({"metrics": [{"metric_name": "http.server.request.duration", "instrument": "histogram", "unit": "s"}]})
        self.assertEqual(
            str(raised.exception),
            "qyl-registry.json metric 'http.server.request.duration' shadows the upstream core metric of the same name")

    def test_metric_group_shadowing_an_upstream_group_is_refused(self):
        del self.core["metrics"][0]
        with self.assertRaises(MergeError) as raised:
            self.run_merge({"metrics": [{"metric_name": "http.server.request.duration", "instrument": "histogram", "unit": "s"}]})
        self.assertEqual(
            str(raised.exception),
            "qyl-registry.json metric group 'metric.http.server.request.duration' shadows the upstream core group of the same id")

    def test_unknown_metric_attribute_reference_is_refused(self):
        with self.assertRaises(MergeError) as raised:
            self.run_merge({"metrics": [{"metric_name": "qyl.m", "instrument": "counter", "unit": "1", "attributes": ["nope"]}]})
        self.assertEqual(str(raised.exception), "qyl metric references unknown attribute 'nope'")

    def _enum_core_attribute(self):
        row = attribute("messaging.system", "core")
        row["type"] = {"members": [{"id": "kafka", "value": "kafka", "stability": "development", "brief": "Apache Kafka"}]}
        self.core["catalog"].append(row)
        return row

    def test_local_attribute_values_extend_an_upstream_enum(self):
        row = self._enum_core_attribute()
        merged, _ = self.run_merge({"local_attribute_values": [
            {"key": "messaging.system", "members": [
                {"id": "masstransit", "value": "masstransit", "stability": "development", "brief": "MassTransit.", "note": "local"}]}]})
        members = next(a for a in merged["catalog"] if a["key"] == "messaging.system")["type"]["members"]
        self.assertEqual([member["value"] for member in members], ["kafka", "masstransit"])
        self.assertEqual(members[0].get("source_registry"), None)
        self.assertEqual(members[1]["source_registry"], "qyl")
        self.assertEqual(members[1]["source_ref"], "qyl-registry.json")
        self.assertEqual(row["type"]["members"][1]["note"], "local")

    def test_local_attribute_values_for_an_unknown_attribute_are_refused(self):
        with self.assertRaises(MergeError) as raised:
            self.run_merge({"local_attribute_values": [{"key": "messaging.system", "members": []}]})
        self.assertEqual(
            str(raised.exception),
            "qyl-registry.json local_attribute_values entry 'messaging.system' names no upstream attribute")

    def test_local_attribute_values_naming_a_qyl_attribute_are_refused(self):
        with self.assertRaises(MergeError) as raised:
            self.run_merge({"local_attribute_values": [{"key": "qyl.thing", "members": []}]})
        self.assertEqual(
            str(raised.exception),
            "qyl-registry.json local_attribute_values entry 'qyl.thing' names a qyl-owned attribute; "
            "declare its members inline under `attributes` instead")

    def test_local_attribute_value_that_landed_upstream_is_refused(self):
        self._enum_core_attribute()
        with self.assertRaises(MergeError) as raised:
            self.run_merge({"local_attribute_values": [
                {"key": "messaging.system", "members": [{"id": "kafka", "value": "kafka"}]}]})
        self.assertEqual(
            str(raised.exception),
            "qyl-registry.json local_attribute_values member 'messaging.system=kafka' now exists upstream; "
            "delete the local declaration")

    def test_local_attribute_values_on_a_non_enum_attribute_are_refused(self):
        with self.assertRaises(MergeError) as raised:
            self.run_merge({"local_attribute_values": [{"key": "http.route", "members": []}]})
        self.assertEqual(
            str(raised.exception),
            "qyl-registry.json local_attribute_values entry 'http.route' names a non-enum upstream attribute")

    def test_entity_associations_are_normalised_to_type_strings(self):
        """Weaver <=0.25.1 emits entity associations as strings, >=0.26.0 as {"type": ...}.

        RegistryLoader.ParseStringArray reads strings, so an unnormalised object shape
        parses as empty and silently drops every metric definition's entities.
        """
        self.core["metrics"][0]["entity_associations"] = [{"type": "container"}, "host"]
        self.core["groups"][0]["entity_associations"] = [{"type": "k8s.pod"}]
        merged, _ = self.run_merge({})
        self.assertEqual(merged["metrics"][0]["entity_associations"], ["container", "host"])
        self.assertEqual(merged["groups"][0]["entity_associations"], ["k8s.pod"])

    def test_an_unrecognised_entity_association_shape_is_refused(self):
        self.core["metrics"][0]["entity_associations"] = [{"name": "container"}]
        with self.assertRaises(MergeError) as raised:
            self.run_merge({})
        self.assertIn("unrecognised entity association", str(raised.exception))



    # ------------------------------------------------------------------ vendor models
    def vendor_model(self, **overrides):
        model = {
            "library": "MassTransit",
            "version": "8.5.10",
            "repository": "https://github.com/MassTransit/MassTransit",
            "ref": "v8.5.10",
            "license": "Apache-2.0",
            "brief": "b",
            "activity_sources": [{"name": "MassTransit", "note": "DiagnosticHeaders.cs:6"}],
            "attributes": [{
                "key": "messaging.masstransit.saga_id",
                "type": "string",
                "stability": "development",
                "brief": "b",
                "note": "LogContextActivityExtensions.cs:129",
            }],
        }
        model.update(overrides)
        return model

    def test_a_vendor_model_is_the_one_door_for_a_non_qyl_key(self):
        merged, qyl_bytes = self.run_merge({"vendor_models": [self.vendor_model()]})
        row = next(row for row in merged["catalog"] if row["key"] == "messaging.masstransit.saga_id")
        self.assertEqual(row["source_registry"], "qyl")
        self.assertEqual(row["source_commit"], hashlib.sha256(qyl_bytes).hexdigest())
        self.assertEqual(row["vendor_library"], "MassTransit")
        self.assertEqual(row["vendor_version"], "8.5.10")
        self.assertEqual(row["vendor_ref"], "v8.5.10")
        self.assertEqual(merged["vendor_scope_names"], ["MassTransit"])
        self.assertEqual(merged["vendor_models"][0]["attribute_keys"], ["messaging.masstransit.saga_id"])

    def test_a_non_qyl_attribute_outside_a_vendor_model_is_still_refused(self):
        with self.assertRaises(MergeError) as raised:
            self.run_merge({"attributes": [{"key": "messaging.masstransit.saga_id", "type": "string"}]})
        self.assertEqual(
            str(raised.exception),
            "qyl-registry.json attribute 'messaging.masstransit.saga_id' is outside the qyl.* namespace; "
            "qyl-owned attributes must start with 'qyl.', and a third-party key belongs in a "
            "declared vendor_models entry")

    def test_a_vendor_model_without_its_pin_is_refused(self):
        with self.assertRaises(MergeError) as raised:
            self.run_merge({"vendor_models": [self.vendor_model(version="")]})
        self.assertIn("is missing 'version'", str(raised.exception))

    def test_a_vendor_attribute_without_a_finding_is_refused(self):
        model = self.vendor_model()
        del model["attributes"][0]["note"]
        with self.assertRaises(MergeError) as raised:
            self.run_merge({"vendor_models": [model]})
        self.assertEqual(
            str(raised.exception),
            "qyl-registry.json vendor_models attribute 'messaging.masstransit.saga_id' carries no finding; "
            "cite the file and line of the library that sets it")

    def test_a_vendor_attribute_shadowing_an_upstream_row_is_refused(self):
        model = self.vendor_model()
        model["attributes"][0]["key"] = "http.route"
        with self.assertRaises(MergeError) as raised:
            self.run_merge({"vendor_models": [model]})
        self.assertEqual(
            str(raised.exception),
            "qyl-registry.json vendor_models entry 'MassTransit' attribute 'http.route' shadows the upstream "
            "core catalog row of the same key")

    def test_a_qyl_owned_key_in_a_vendor_model_is_refused(self):
        model = self.vendor_model()
        model["attributes"][0]["key"] = "qyl.project.id"
        with self.assertRaises(MergeError) as raised:
            self.run_merge({"vendor_models": [model]})
        self.assertIn("declares qyl-owned attribute 'qyl.project.id'", str(raised.exception))

    def test_two_vendor_models_may_not_claim_the_same_key(self):
        with self.assertRaises(MergeError) as raised:
            self.run_merge({"vendor_models": [self.vendor_model(), self.vendor_model(library="Other")]})
        self.assertEqual(
            str(raised.exception),
            "qyl-registry.json vendor_models attribute 'messaging.masstransit.saga_id' is declared by both "
            "'MassTransit' and 'Other'")

    def test_a_library_that_emits_no_vendor_key_still_declares_its_source_name(self):
        merged, _ = self.run_merge({"vendor_models": [self.vendor_model(
            library="MySqlConnector",
            activity_sources=[{"name": "MySqlConnector", "note": "ActivitySourceHelper.cs:96"}],
            attributes=[])]})
        self.assertEqual(merged["vendor_scope_names"], ["MySqlConnector"])
        self.assertEqual(merged["vendor_models"][0]["attribute_keys"], [])

    # ------------------------------------------------- the closed namespace value set
    def test_a_namespace_missing_from_the_closed_value_set_fails_the_merge(self):
        qyl = {"vendor_models": [self.vendor_model()]}
        qyl["attributes"] = [self.namespace_attribute(qyl, drop=["messaging"])]
        with self.assertRaises(MergeError) as raised:
            self.run_merge(qyl)
        self.assertEqual(
            str(raised.exception),
            "qyl.attribute.namespace does not list the merged catalog's namespaces: "
            "missing ['messaging'], unknown []; the value set is closed, so update it in qyl-registry.json")

    def test_a_namespace_the_catalog_does_not_have_fails_the_merge(self):
        qyl = {}
        qyl["attributes"] = [self.namespace_attribute(qyl, extra=["quartz"])]
        with self.assertRaises(MergeError) as raised:
            self.run_merge(qyl)
        self.assertIn("missing [], unknown ['quartz']", str(raised.exception))


if __name__ == "__main__":
    unittest.main()
