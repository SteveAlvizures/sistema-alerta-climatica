"""Insert-only migration from legacy HTTP contracts. Run before switching Gateway routes.

Credentials are read from environment; data sources must be quiesced. Internal
imports use the destination containers' own service key, never foreign DB credentials.
"""
import json
import os
import re
import subprocess
import sys
import urllib.request


def main():
    base = os.environ["MIGRATION_SOURCE_URL"].rstrip("/")
    project = os.environ.get("MIGRATION_COMPOSE_PROJECT", "climate-alert-phase3")
    if not re.fullmatch(r"[a-z0-9][a-z0-9_-]*", project):
        raise RuntimeError("Invalid Compose project")
    def get(path, body=None):
        headers = {"Content-Type": "application/json"}
        if token:
            headers["Authorization"] = "Bearer " + token
        request = urllib.request.Request(base+path, data=json.dumps(body).encode() if body else None, headers=headers)
        with urllib.request.urlopen(request, timeout=40) as response:
            return json.load(response)
    token = None
    token = get("/api/auth/login", {"username": os.environ["MIGRATION_USERNAME"], "password": os.environ["MIGRATION_PASSWORD"]})["accessToken"]
    def destination(service, path, payload, port):
        ids = subprocess.check_output(["docker", "ps", "--filter", "label=com.docker.compose.project="+project,
            "--filter", "label=com.docker.compose.service="+service, "--format", "{{.ID}}"], text=True).split()
        if len(ids) != 1:
            raise RuntimeError("Expected exactly one destination container")
        command = 'curl -fsS -X POST -H "Content-Type: application/json" -H "X-Internal-Service-Key: $AUTH_INTERNAL_KEY" --data-binary @- http://localhost:'+str(port)+path
        result = subprocess.run(["docker", "exec", "-i", ids[0], "sh", "-c", command],
            input=json.dumps(payload), text=True, capture_output=True)
        if result.returncode:
            raise RuntimeError("Internal import rejected; review destination using its correlation ID")
    sensors = get("/api/sensors")
    destination("sensor-service", "/internal/import", {"sensors": sensors, "readings": []}, 8080)
    total = None
    seen = set()
    pending = []
    page = 1
    while True:
        batch = get(f"/api/sensor-readings?page={page}&pageSize=50")
        if total is not None and batch["totalCount"] != total:
            raise RuntimeError("Source changed during migration; stop writes and repeat safely")
        total = batch["totalCount"]
        for row in batch["data"]:
            if row["id"] in seen:
                raise RuntimeError("Unstable source pagination")
            seen.add(row["id"])
            pending.append(row)
        if len(pending) >= 500 or not batch["hasNext"]:
            destination("sensor-service", "/internal/import", {"sensors": [], "readings": pending}, 8080)
            pending = []
        if not batch["hasNext"]:
            break
        page += 1
    if len(seen) != total:
        raise RuntimeError("Incomplete source")
    def all_pages(path):
        result = []
        page = 1
        while True:
            value = get(path+f"?page={page}&pageSize=50")
            result.extend(value["data"])
            if not value["hasNext"]:
                return result
            page += 1
    rules = get("/api/alert-rules")
    alerts = all_pages("/api/alerts")
    events = all_pages("/api/events")
    destination("alert-service", "/internal/migration", {"rules": rules, "events": events, "alerts": alerts}, 3003)
    print(json.dumps({"sensors": len(sensors), "readings": len(seen), "rules": len(rules),
        "alerts": len(alerts), "events": len(events), "mode": "insert-only"}))


if __name__ == "__main__":
    try:
        main()
    except Exception:
        print("HTTP migration failed; no source data was changed. Review configuration/destination privately.", file=sys.stderr)
        raise SystemExit(1) from None
