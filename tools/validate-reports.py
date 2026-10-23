"""Non-destructive HTTP smoke test. Credentials come only from environment variables."""
import json
import os
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timedelta
from pathlib import Path


def main():
    base = os.environ["REPORT_E2E_URL"].rstrip("/")
    output = Path(os.environ.get("REPORT_E2E_OUTPUT", "tmp/report-evidence"))
    output.mkdir(parents=True, exist_ok=True)
    evidence = []

    def call(path, method="GET", body=None, token=None, expected=200):
        headers = {"Content-Type": "application/json"}
        if token:
            headers["Authorization"] = "Bearer " + token
        request = urllib.request.Request(base + path, method=method, headers=headers,
            data=json.dumps(body).encode() if body is not None else None)
        start = time.monotonic()
        try:
            with urllib.request.urlopen(request, timeout=40) as response:
                code, data, response_headers = response.status, response.read(), response.headers
        except urllib.error.HTTPError as response:
            code, data, response_headers = response.code, response.read(), response.headers
        evidence.append({"path": path, "method": method, "status": code,
                         "durationMs": round((time.monotonic()-start)*1000), "responseBytes": len(data)})
        if code != expected:
            raise RuntimeError(f"Unexpected HTTP {code} for {method} {path}; inspect the service using its correlation ID.")
        return data, response_headers

    raw, _ = call("/api/auth/login", "POST", {"username": os.environ["REPORT_E2E_USERNAME"],
                                            "password": os.environ["REPORT_E2E_PASSWORD"]})
    token = json.loads(raw)["accessToken"]
    call("/api/reports/types", expected=401)
    call("/api/reports/types", token="invalid", expected=401)
    raw, _ = call("/api/reports/types", token=token)
    types = json.loads(raw)
    if len(types) != 8:
        raise RuntimeError("Expected eight report types.")
    raw, _ = call("/api/sensor-readings/latest?page=1&pageSize=50", token=token)
    latest = json.loads(raw)["data"]
    # Explicit, reproducible narrow range. Override to exercise a larger data set.
    latest_at = max((row["measuredAt"] for row in latest), default=None)
    cutoff = os.environ.get("REPORT_E2E_FROM") or (
        (datetime.fromisoformat(latest_at.replace("Z", "+00:00"))-timedelta(minutes=5)).isoformat()
        if latest_at else None)
    for kind in types:
        scoped = kind["id"] in ("readings", "statistics", "executive") or bool(os.environ.get("REPORT_E2E_FROM"))
        filters = {"dateFrom": cutoff} if kind["supportsDates"] and scoped and cutoff else {}
        raw, _ = call("/api/reports", "POST", {"type": kind["id"], "filters": filters}, token, 201)
        metadata = json.loads(raw)
        pdf, headers = call(f"/api/reports/{metadata['id']}/download", token=token)
        if (metadata["state"] != "Completed" or not pdf.startswith(b"%PDF-") or
                len(pdf) != metadata["size"] or headers.get_content_type() != "application/pdf"):
            raise RuntimeError("PDF verification failed.")
        (output / f"{kind['id']}.pdf").write_bytes(pdf)
        evidence.append({"type": kind["id"], "id": metadata["id"], "pdfBytes": len(pdf),
                         "state": metadata["state"], "filters": filters})
        print(kind["id"], metadata["id"], len(pdf), "bytes", flush=True)
    call("/api/reports", token=token)
    call("/api/reports", "POST", {"type": "readings", "filters": {
        "dateFrom": "2026-10-23T00:00:00Z", "dateTo": "2026-10-22T00:00:00Z"}}, token, 400)
    (output / "http-results.json").write_text(json.dumps(evidence, indent=2), encoding="utf8")
    print("Report E2E passed:", len(evidence), "checks")


if __name__ == "__main__":
    try:
        main()
    except Exception:
        # Do not echo credential-bearing requests, responses or exceptions.
        print("Report E2E failed. Check configuration, filters and service health.", file=sys.stderr)
        raise SystemExit(1) from None
