import hashlib
import unittest
from datetime import datetime, timedelta, timezone
from uuid import uuid4

import httpx
import jwt
from fastapi.testclient import TestClient
from app import Settings, create_app


class AuditTests(unittest.TestCase):
    def setUp(self):
        self.settings = Settings(legacy_audit_url="")
        self.user = str(uuid4())
        self.role = "Administrator"
        self.active = True
        def handler(request):
            return httpx.Response(200, json={"id": self.user, "isActive": self.active, "role": self.role})
        self.context = TestClient(create_app(self.settings, httpx.MockTransport(handler)))
        self.client = self.context.__enter__()
        self.collection = "audit_test_" + uuid4().hex
        self.client.app.state.rows = self.client.app.state.mongo[self.settings.audit_mongo_database][self.collection]
        token = jwt.encode({"sub": self.user, "exp": datetime.now(timezone.utc)+timedelta(minutes=5),
            "iss": self.settings.jwt_issuer, "aud": self.settings.jwt_audience},
            hashlib.sha256(self.settings.jwt_key.get_secret_value().encode()).digest(), algorithm="HS256")
        self.headers = {"Authorization": "Bearer " + token}
        self.internal = {"X-Internal-Service-Key": self.settings.auth_internal_key.get_secret_value()}
        identity = {"id": self.user, "name": "Audit test", "username": "audit-test", "passwordHash": "must-not-be-stored"}
        self.event = {"id": str(uuid4()), "actor": identity, "target": identity, "action": "Login",
            "description": "Test login", "affectedRecordId": self.user, "occurredAt": "2026-10-23T00:00:00Z"}

    def tearDown(self):
        assert self.collection.startswith("audit_test_")
        self.client.app.state.rows.drop()
        self.context.__exit__(None, None, None)

    def test_idempotence_and_safe_projection(self):
        for _ in range(2):
            self.assertEqual(self.client.post("/internal/auth-events", json=self.event, headers=self.internal).status_code, 204)
        result = self.client.get("/api/audit-actions", headers=self.headers).json()
        self.assertEqual(result["totalCount"], 1)
        self.assertNotIn("passwordHash", str(result))
        self.assertEqual(result["data"][0]["affectedEntity"], "User")

    def test_missing_session(self):
        self.assertEqual(self.client.get("/api/audit-actions").status_code, 401)

    def test_invalid_session(self):
        self.assertEqual(self.client.get("/api/audit-actions", headers={"Authorization": "Bearer invalid"}).status_code, 401)

    def test_operator_forbidden(self):
        self.role = "Operator"
        self.assertEqual(self.client.get("/api/audit-actions", headers=self.headers).status_code, 403)

    def test_consultation_forbidden(self):
        self.role = "ConsultationUser"
        self.assertEqual(self.client.get("/api/audit-actions", headers=self.headers).status_code, 403)

    def test_inactive_identity(self):
        self.active = False
        self.assertEqual(self.client.get("/api/audit-actions", headers=self.headers).status_code, 401)

    def test_internal_key_required(self):
        self.assertEqual(self.client.post("/internal/auth-events", json=self.event).status_code, 401)

    def test_filters(self):
        self.client.post("/internal/auth-events", json=self.event, headers=self.internal)
        self.assertEqual(self.client.get("/api/audit-actions?action=Logout", headers=self.headers).json()["totalCount"], 0)
        self.assertEqual(self.client.get("/api/audit-actions?user=audit-test&entity=User&pageSize=1", headers=self.headers).json()["totalCount"], 1)

    def test_bad_date_range(self):
        self.assertEqual(self.client.get("/api/audit-actions?from=2026-10-24T00:00:00Z&to=2026-10-23T00:00:00Z", headers=self.headers).status_code, 400)

    def test_bad_page_matches_legacy_validation_contract(self):
        self.assertEqual(self.client.get("/api/audit-actions?page=0", headers=self.headers).status_code, 400)

    def test_rejects_naive_event_date(self):
        self.event["occurredAt"] = "2026-10-23T00:00:00"
        self.assertEqual(self.client.post("/internal/auth-events", json=self.event, headers=self.internal).status_code, 400)


if __name__ == "__main__":
    unittest.main()
