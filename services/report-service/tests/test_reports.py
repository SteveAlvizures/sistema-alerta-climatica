import asyncio
import hashlib
import io
import os
from datetime import timedelta
from pathlib import Path
from uuid import uuid4

import httpx
import jwt
import pytest
from fastapi.testclient import TestClient
from pydantic import ValidationError
from pypdf import PdfReader

from app.application import context_for
from app.config import Settings
from app.errors import ReportError
from app.integrations import ReadingClient
from app.main import create_app
from app.models import ReportRequest, ReportType, TITLES
from app.persistence import ReportStore, now
from app.rendering import render

USER = str(uuid4())
COMMUNITY = str(uuid4())
SENSOR = str(uuid4())
KEY = "report-test-signing-key-with-more-than-32-characters"


class PrivateHeaders(dict):
    def __repr__(self):
        return "<authorization headers redacted>"


DATA = {
    "/api/communities": [{"id": COMMUNITY, "name": "Comunidad de prueba", "isActive": True,
                          "sensorCount": 1, "municipality": "Cobán", "country": "Guatemala"}],
    "/api/sensors": [{"id": SENSOR, "communityId": COMMUNITY, "communityName": "Comunidad de prueba",
                      "code": "TEMP-01", "name": "Temperatura norte", "type": "Temperature", "status": "Active"}],
    "/api/sensor-readings": [{"sensorId": SENSOR, "sensorCode": "TEMP-01", "sensorName": "Temperatura norte",
        "communityName": "Comunidad de prueba", "measuredAt": "2026-10-22T12:00:00Z", "variable": "Temperature",
        "value": 24.5, "unit": "°C", "sensorStatusAtMeasurement": "Active"}],
    "/api/alerts": [{"detectedAt": "2026-10-22T12:00:00Z", "communityName": "Comunidad de prueba",
        "sensorCode": "TEMP-01", "phenomenon": "Drought", "level": "Yellow", "detectedValue": 24.5,
        "unit": "°C", "activationPoint": 24, "message": "Revisar condiciones de temperatura", "status": "Open"}],
    "/api/events": [{"occurredAt": "2026-10-22T12:00:00Z", "communityName": "Comunidad de prueba",
        "phenomenon": "Drought", "level": "Yellow", "description": "Evento de temperatura",
        "status": "Open", "alertCount": 1}],
}


@pytest.fixture
def settings():
    return Settings(jwt_key=KEY, auth_internal_key="report-test-internal-key-at-least-32-characters",
        report_mongo_host=os.environ.get("TEST_MONGO_HOST", "report-mongodb"),
        report_mongo_database="reports_test", report_mongo_user="report_test",
        report_mongo_password=os.environ.get("TEST_MONGO_PASSWORD", "isolated-test-only-password"))


def token(**changes):
    return jwt.encode({"sub": USER, "iss": "climate-alert-api", "aud": "climate-alert-client",
                       "exp": now() + timedelta(minutes=5), **changes}, hashlib.sha256(KEY.encode()).digest(), algorithm="HS256")


@pytest.fixture
def rig(settings):
    control = {"role": "Administrator", "active": True, "empty": False, "failure": False, "calls": []}

    def handle(request):
        control["calls"].append((request.url.path, dict(request.headers)))
        if request.url.path == "/internal/identity/current":
            return httpx.Response(200, json={"id": USER, "name": "Operador de prueba", "isActive": control["active"],
                "role": control["role"], "permissions": ["OperateSystem"] if control["role"] != "ConsultationUser" else []})
        if request.url.path == "/health/ready":
            return httpx.Response(200)
        if control["failure"]:
            return httpx.Response(503)
        rows = [] if control["empty"] else DATA[request.url.path.replace("/latest", "")]
        return httpx.Response(200, json={"data": rows, "totalCount": len(rows), "hasNext": False})

    with TestClient(create_app(settings, httpx.MockTransport(handle))) as client:
        store = client.app.state.store
        assert store.db.name.endswith("_test")  # Never clear a real application's database.
        store.reports.delete_many({})
        store.db.pdfs.files.delete_many({})
        store.db.pdfs.chunks.delete_many({})
        yield client, store, control, PrivateHeaders({"Authorization": "Bearer " + token()})


@pytest.mark.parametrize("kind", list(ReportType))
def test_real_pdf_each_type_and_repeat_download(rig, kind):
    client, store, control, headers = rig
    response = client.post("/api/reports", headers=headers, json={"type": kind})
    assert response.status_code == 201, response.text
    item = response.json()
    assert item["state"] == "Completed" and item["size"] > 1000 and "fileId" not in item
    for _ in range(2):
        pdf = client.get(f"/api/reports/{item['id']}/download", headers=headers)
        assert pdf.status_code == 200 and pdf.headers["content-type"] == "application/pdf"
        assert pdf.content.startswith(b"%PDF-") and len(pdf.content) == item["size"]
    reader = PdfReader(io.BytesIO(pdf.content))
    text = "".join(page.extract_text() for page in reader.pages)
    assert TITLES[kind] in text and "VIGÍA RURAL" in text and "Operador de prueba" in text
    assert "Comunidad de prueba" in text if kind not in ("statistics", "executive") else "24.5" in text
    assert store.owned(item["id"], USER)["downloadedAt"] is not None
    assert client.get("/api/reports", headers=headers).json()["totalCount"] == 1
    assert client.get(f"/api/reports/{item['id']}/status", headers=headers).json()["state"] == "Completed"
    paths = [p for p, _ in control["calls"] if p.startswith("/api/")]
    assert len(paths) == len(set(paths))  # No per-sensor N+1.
    out = Path("/evidence")
    if out.exists():
        (out / f"test-{kind}.pdf").write_bytes(pdf.content)


@pytest.mark.parametrize("body", [{"type": "unknown"}, {"type": "readings", "filters": {"sensorId": "bad"}},
    {"type": "readings", "filters": {"dateFrom": "2026-10-23T00:00:00Z", "dateTo": "2026-10-22T00:00:00Z"}},
    {"type": "alerts", "filters": {"status": "Active"}}, {"type": "events", "filters": {"variable": "Temperature"}},
    {"type": "sensors", "filters": {"dateFrom": "2026-10-22T00:00:00Z"}},
    {"type": "readings", "filters": {"dateFrom": "2026-10-22T00:00:00"}},
    {"type": "communities", "filters": {"unexpected": "bad"}}])
def test_bad_requests(rig, body):
    client, _, _, headers = rig
    assert client.post("/api/reports", headers=headers, json=body).status_code == 400


@pytest.mark.parametrize("case", ["missing", "malformed", "expired", "wrong-audience", "wrong-issuer"])
def test_invalid_auth(rig, case):
    client, _, _, _ = rig
    bearer = {"missing": None, "malformed": "bad", "expired": token(exp=now()-timedelta(seconds=2)),
              "wrong-audience": token(aud="other"), "wrong-issuer": token(iss="other")}[case]
    assert client.get("/api/reports/types", headers={"Authorization": "Bearer " + bearer} if bearer else {}).status_code == 401


def test_current_identity_permissions_and_owner(rig):
    client, store, control, headers = rig
    control["role"] = "ConsultationUser"
    assert client.get("/api/reports/types", headers=headers).status_code == 403
    control["role"] = "Operator"
    assert client.get("/api/reports/types", headers=headers).status_code == 200
    control["active"] = False
    assert client.get("/api/reports/types", headers=headers).status_code == 401
    control["active"] = True
    item = store.create("communities", {}, {"id": str(uuid4()), "name": "Otro usuario"}, str(uuid4()))
    assert client.get(f"/api/reports/{item['_id']}/download", headers=headers).status_code == 404


def test_failure_persisted_and_safe(rig):
    client, store, control, headers = rig
    control["failure"] = True
    result = client.post("/api/reports", headers=headers, json={"type": "communities"})
    assert result.status_code == 502
    item = store.reports.find_one()
    assert item["state"] == "Failed" and item["error"] and "fileId" not in item
    assert "report-mongodb" not in result.text


def test_metadata_missing_pdf_and_busy(rig):
    client, store, _, headers = rig
    assert client.get(f"/api/reports/{uuid4()}", headers=headers).status_code == 404
    item = store.create("communities", {}, {"id": USER, "name": "Test"}, str(uuid4()))
    path = f"/api/reports/{item['_id']}/download"
    assert client.get(path, headers=headers).status_code == 409
    store.reports.update_one({"_id": item["_id"]}, {"$set": {"state": "Completed"}})
    assert client.get(path, headers=headers).status_code == 409
    store.complete(item, b"%PDF-test")
    file_id = store.reports.find_one()["fileId"]
    store.files.delete(file_id)
    assert client.get(path, headers=headers).status_code == 404
    client.app.state.busy = True
    assert client.post("/api/reports", headers=headers, json={"type": "sensors"}).status_code == 429
    client.app.state.busy = False


def test_mongo_indexes_reconnect_history_paging(rig, settings):
    client, store, _, headers = rig
    for _ in range(12):
        store.create("communities", {}, {"id": USER, "name": "Test"}, str(uuid4()))
    other = ReportStore(settings)
    assert other.reports.count_documents({"user.id": USER}) == 12
    other.client.close()
    assert len(store.reports.index_information()) >= 6
    page = client.get("/api/reports?page=2&pageSize=10&type=communities&state=Pending", headers=headers).json()
    assert len(page["data"]) == 2 and page["hasPrevious"] and not page["hasNext"]
    assert client.get("/api/reports?page=0", headers=headers).status_code == 400


def test_empty_pdf(rig):
    client, _, control, headers = rig
    control["empty"] = True
    response = client.post("/api/reports", headers=headers, json={"type": "readings", "filters": {"communityId": COMMUNITY}})
    pdf = client.get(f"/api/reports/{response.json()['id']}/download", headers=headers)
    text = PdfReader(io.BytesIO(pdf.content)).pages[0].extract_text()
    assert "No se encontraron datos" in text and COMMUNITY in text


def test_multi_page_and_html_escaping():
    rows = [{**DATA["/api/communities"][0], "name": f"Comunidad {n} <script> no ejecutar & " + "territorio "*8} for n in range(130)]
    context = context_for(ReportRequest(type="communities"), {"communities": rows}, {"name": "Prueba"}, str(uuid4()))
    pdf = asyncio.run(render(context, 20, 10_000_000))
    reader = PdfReader(io.BytesIO(pdf))
    assert len(reader.pages) >= 4
    assert "Comunidad 129" in "".join(p.extract_text() for p in reader.pages)
    assert "Comunidad" in reader.pages[1].extract_text() and "Página 2" in reader.pages[1].extract_text()
    if Path("/evidence").exists():
        Path("/evidence/test-multipage.pdf").write_bytes(pdf)


@pytest.mark.parametrize("failure,expected", [("timeout", 504), ("too-many", 422), ("partial", 502)])
def test_bounded_http(failure, expected):
    def handler(request):
        if failure == "timeout":
            raise httpx.ReadTimeout("private upstream", request=request)
        return httpx.Response(200, json={"data": [{}], "totalCount": 9000 if failure == "too-many" else 2, "hasNext": False})

    async def run():
        async with httpx.AsyncClient(transport=httpx.MockTransport(handler)) as http:
            with pytest.raises(ReportError) as error:
                await ReadingClient(http, "http://fixture", 100).fetch({}, {})
            assert error.value.status == expected
    asyncio.run(run())


def test_stats_do_not_invent_accumulated_rainfall():
    context = context_for(ReportRequest(type="statistics"), {"readings": DATA["/api/sensor-readings"], "alerts": []}, {"name": "Test"}, "test")
    assert context["kpis"][-1] == ("Lluvia acumulada", "N/D")
    row = context["tables"][0]["rows"][0]
    assert row[3:6] == ["24.5", "24.5", "24.5"]


def test_validation_without_database():
    with pytest.raises(ValidationError):
        ReportRequest(type="communities", filters={"sensorId": str(uuid4())})


def test_http_pagination_headers_and_filters():
    requests = []

    def handle(request):
        requests.append(request)
        page = int(request.url.params["page"])
        return httpx.Response(200, json={"data": [{"id": page}], "totalCount": 2, "hasNext": page == 1})

    async def run():
        async with httpx.AsyncClient(transport=httpx.MockTransport(handle)) as http:
            rows = await ReadingClient(http, "http://fixture", 100).fetch({"communityId": COMMUNITY},
                {"Authorization": "Bearer fixture", "X-Correlation-ID": "test-correlation"})
            assert len(rows) == 2
    asyncio.run(run())
    assert len(requests) == 2
    assert all(r.url.params["communityId"] == COMMUNITY and r.headers["X-Correlation-ID"] == "test-correlation" for r in requests)


def test_stale_pending_becomes_failed(rig):
    client, store, _, headers = rig
    item = store.create("communities", {}, {"id": USER, "name": "Test"}, str(uuid4()))
    store.reports.update_one({"_id": item["_id"]}, {"$set": {"requestedAt": now()-timedelta(minutes=3)}})
    response = client.get(f"/api/reports/{item['_id']}", headers=headers)
    assert response.json()["state"] == "Failed"


def test_render_timeout_terminates_child():
    async def run():
        with pytest.raises(TimeoutError):
            await render({}, 0.001, 10_000_000)
    asyncio.run(run())


def test_missing_sensor_count_is_not_reported_as_zero():
    row = {k: v for k, v in DATA["/api/communities"][0].items() if k != "sensorCount"}
    context = context_for(ReportRequest(type="communities"), {"communities": [row]}, {"name": "Test"}, "test")
    assert context["kpis"][-1] == ("Sensores registrados", "N/D")
