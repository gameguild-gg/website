"""Offline regression checks for the pinned FedRAMP source generator."""
import importlib.util
import unittest
from pathlib import Path
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location("fedramp_catalog", Path(__file__).with_name("build-fedramp-catalog.py"))
CATALOG = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CATALOG)
SCHEMA_URI = "https://fedramp.gov/schemas/example.json"


class SourceSecurityTests(unittest.TestCase):
    def test_validation_is_active_under_optimization(self):
        with self.assertRaises(ValueError):
            CATALOG.require(False, "Invalid source")

    def test_untrusted_destinations_never_connect(self):
        for uri in ("file:///tmp/schema.json", "http://fedramp.gov/schemas/example.json",
                    "https://fedramp.gov.evil.test/schemas/example.json", "https://user@fedramp.gov/schemas/example.json",
                    "https://fedramp.gov:8443/schemas/example.json", "https://fedramp.gov/schemas/example.json?token=x"):
            with self.subTest(uri=uri), patch.object(CATALOG.http.client, "HTTPSConnection") as connect:
                with self.assertRaises(ValueError):
                    CATALOG.fetch_schema(uri)
                connect.assert_not_called()

    def test_official_download_uses_https_and_is_bounded(self):
        with patch.object(CATALOG.http.client, "HTTPSConnection") as connect:
            response = connect.return_value.getresponse.return_value
            response.status = 200
            response.read.return_value = b"{}"
            self.assertEqual(b"{}", CATALOG.fetch_schema(SCHEMA_URI))
            connect.assert_called_once_with("www.fedramp.gov", timeout=45)
            response.read.assert_called_once_with(1048577)
            connect.return_value.close.assert_called_once()

    def test_external_redirect_is_rejected_before_connecting(self):
        with patch.object(CATALOG.http.client, "HTTPSConnection") as connect:
            response = connect.return_value.getresponse.return_value
            response.status = 302
            response.getheader.return_value = "https://evil.test/schemas/example.json"
            with self.assertRaises(ValueError):
                CATALOG.fetch_schema(SCHEMA_URI)
            connect.assert_called_once_with("www.fedramp.gov", timeout=45)
            connect.return_value.close.assert_called_once()

    def test_redirects_are_limited(self):
        with patch.object(CATALOG.http.client, "HTTPSConnection") as connect:
            response = connect.return_value.getresponse.return_value
            response.status = 302
            response.getheader.return_value = SCHEMA_URI
            with self.assertRaises(ValueError):
                CATALOG.fetch_schema(SCHEMA_URI)
            self.assertEqual(4, connect.call_count)

    def test_oversized_or_failed_downloads_are_rejected(self):
        for status, content in ((404, b"{}"), (200, b"x" * 1048577)):
            with self.subTest(status=status), patch.object(CATALOG.http.client, "HTTPSConnection") as connect:
                response = connect.return_value.getresponse.return_value
                response.status = status
                response.read.return_value = content
                with self.assertRaises(ValueError):
                    CATALOG.fetch_schema(SCHEMA_URI)
                connect.return_value.close.assert_called_once()

    def test_dtd_and_entities_are_rejected(self):
        from defusedxml.common import DTDForbidden
        with self.assertRaises(DTDForbidden):
            CATALOG.ET.fromstring(b'<!DOCTYPE root [<!ENTITY item "expansion">]><root>&item;</root>',
                                  forbid_dtd=True, forbid_entities=True, forbid_external=True)


if __name__ == "__main__":
    unittest.main()
