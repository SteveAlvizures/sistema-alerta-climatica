from collections import Counter, defaultdict
from datetime import datetime, timezone
from statistics import mean

from app.integrations import AlertClient, CommunityClient, EventClient, ReadingClient, SensorClient
from app.models import ReportRequest, ReportType, TITLES

LABELS = {"Temperature": "Temperatura", "RelativeHumidity": "Humedad relativa", "Humidity": "Humedad",
          "WindSpeed": "Viento", "RainfallLevel": "Nivel de lluvia", "Rainfall": "Lluvia",
          "RiverOrReservoirLevel": "Nivel de río / embalse", "RiverLevel": "Río", "ReservoirLevel": "Embalse",
          "SmokeConcentration": "Concentración de humo", "SmokeFire": "Humo / fuego",
          "OtherEnvironmental": "Otra variable", "Active": "Activo", "Inactive": "Inactivo",
          "Open": "Abierto", "Acknowledged": "Reconocida", "Closed": "Cerrado", "Green": "Normal",
          "Yellow": "Precaución", "Orange": "Alerta", "Red": "Emergencia", "Flood": "Inundación",
          "Drought": "Sequía", "Storm": "Tormenta", "Frost": "Helada", "Wildfire": "Incendio"}


def display(value):
    if value is None or value == "":
        return "N/D"
    if isinstance(value, float):
        return f"{value:,.4f}".rstrip("0").rstrip(".")
    if isinstance(value, str) and len(value) > 18 and value[10:11] == "T":
        try:
            return datetime.fromisoformat(value.replace("Z", "+00:00")).astimezone(timezone.utc).strftime("%d/%m/%Y %H:%M:%S")
        except ValueError:
            pass
    return LABELS.get(str(value), str(value))


def table(title, headings, rows):
    return {"title": title, "headings": headings, "rows": [[display(v) for v in row] for row in rows]}


async def collect(request: ReportRequest, settings, http, headers):
    """Compose only the sources required by this type, forwarding user identity by HTTP.

    Inventory reflects query time; date ranges apply to activity. Fetching latest
    readings as a batch avoids per-sensor HTTP calls for the status report.
    """
    f = request.filters.model_dump(mode="json", exclude_none=True)
    kind = request.type
    clients = {
        "communities": CommunityClient(http, settings.community_api_url, settings.report_max_rows),
        "sensors": SensorClient(http, settings.sensor_api_url, settings.report_max_rows),
        "readings": ReadingClient(http, settings.reading_api_url, settings.report_max_rows),
        "alerts": AlertClient(http, settings.alert_api_url, settings.report_max_rows),
        "events": EventClient(http, settings.event_api_url, settings.report_max_rows)}
    needed = {"communities": ["communities"], "sensors": ["sensors"], "readings": ["readings"],
              "alerts": ["alerts"], "events": ["events"], "sensor-status": ["sensors", "readings"],
              "statistics": ["readings", "alerts"],
              "executive": ["communities", "sensors", "readings", "alerts", "events"]}[kind]
    data = {}
    for name in needed:
        keys = {"communities": [], "sensors": ["communityId", "variable"],
                "readings": ["communityId", "sensorId", "variable", "dateFrom", "dateTo"],
                "alerts": ["communityId", "sensorId", "variable", "dateFrom", "dateTo"],
                "events": ["communityId", "dateFrom", "dateTo"]}[name]
        params = {k: v for k, v in f.items() if k in keys}
        if name == "events":
            params = { {"dateFrom": "from", "dateTo": "to"}.get(k, k): v for k, v in params.items()}
        if f.get("status"):
            if name in ("communities", "sensors"):
                params["isActive"] = str(f["status"] == "Active").lower()
            elif name in ("alerts", "events"):
                params["status"] = f["status"]
        rows = (await clients[name].latest(params, headers) if kind == ReportType.sensor_status and name == "readings"
                else await clients[name].fetch(params, headers))
        # These selectors are not offered by every source API; apply after bounded retrieval.
        if name == "communities" and f.get("communityId"):
            rows = [r for r in rows if r["id"] == f["communityId"]]
        if name in ("sensors", "events") and f.get("sensorId"):
            rows = [r for r in rows if r.get("sensorId" if name == "events" else "id") == f["sensorId"]]
        if name == "readings" and kind == ReportType.readings and f.get("status"):
            rows = [r for r in rows if r.get("sensorStatusAtMeasurement") == f["status"]]
        data[name] = rows
    return data


def context_for(request: ReportRequest, data, user, correlation):
    kind = request.type
    tables, kpis, notes = [], [], ["N/D: dato no disponible en la fuente. Fechas en UTC. Corte de consulta, no una instantánea transaccional entre servicios."]
    if kind == ReportType.communities:
        rows = data["communities"]
        tables.append(table("Directorio territorial", ["Comunidad", "Municipio", "Departamento", "País", "Coordenadas", "Estado", "Sensores"],
            [[r["name"], r.get("municipality"), r.get("department"), r.get("country"),
              f"{display(r.get('latitude'))}, {display(r.get('longitude'))}",
              "Active" if r["isActive"] else "Inactive", r.get("sensorCount")] for r in rows]))
        counts = [r.get("sensorCount") for r in rows]
        sensor_count = sum(counts) if all(value is not None for value in counts) else "N/D"
        kpis = [("Comunidades", len(rows)), ("Activas", sum(r["isActive"] for r in rows)),
                ("Sensores registrados", sensor_count)]
    elif kind == ReportType.sensors:
        rows = data["sensors"]
        tables.append(table("Inventario de equipos", ["Código / nombre", "Tipo", "Comunidad", "Ubicación", "Unidad", "Estado", "Instalación"],
            [[r["code"] + " / " + r["name"], r.get("type"), r.get("communityName"), r.get("location"),
              r.get("unit"), r.get("status"), r.get("installationDate")] for r in rows]))
        kpis = [("Sensores", len(rows)), ("Activos", sum(r["status"] == "Active" for r in rows))]
    elif kind == ReportType.readings:
        rows = data["readings"]
        tables.append(table("Registro de mediciones", ["Sensor", "Comunidad", "Fecha y hora UTC", "Variable", "Valor", "Unidad", "Estado al medir"],
            [[r["sensorCode"] + " / " + r["sensorName"], r["communityName"], r["measuredAt"], r["variable"],
              r["value"], r["unit"], r.get("sensorStatusAtMeasurement")] for r in rows]))
        kpis = [("Lecturas", len(rows)), ("Sensores con datos", len({r["sensorId"] for r in rows}))]
    elif kind == ReportType.alerts:
        rows = data["alerts"]
        tables.append(table("Detalle de alertas", ["Fecha UTC / comunidad", "Sensor / fenómeno", "Nivel", "Valor", "Umbral", "Mensaje", "Estado"],
            [[display(r["detectedAt"]) + " / " + r["communityName"], r["sensorCode"] + " / " + display(r["phenomenon"]),
              r["level"], f"{r['detectedValue']} {r['unit']}",
              (f"{display(r.get('minValue'))} - {display(r.get('maxValue'))}" if r.get("usesRange") else
               f"{r.get('comparisonOperator') or ''} {r['activationPoint']}") + " " + r["unit"], r["message"], r["status"]] for r in rows]))
        kpis = [("Alertas", len(rows)), ("Abiertas", sum(r["status"] == "Open" for r in rows))]
    elif kind == ReportType.events:
        rows = data["events"]
        tables.append(table("Historial operativo", ["Fecha UTC", "Comunidad / sensor", "Fenómeno / nivel", "Descripción", "Estado", "Alertas asociadas"],
            [[r["occurredAt"], r["communityName"] + " / " + display(r.get("sensorCode")),
              display(r["phenomenon"]) + " / " + display(r["level"]), r["description"], r["status"], r["alertCount"]] for r in rows]))
        kpis = [("Eventos", len(rows)), ("Abiertos", sum(r["status"] == "Open" for r in rows))]
    elif kind == ReportType.sensor_status:
        latest = {}
        for row in data["readings"]:
            if row["sensorId"] not in latest or row["measuredAt"] > latest[row["sensorId"]]["measuredAt"]:
                latest[row["sensorId"]] = row
        rows = data["sensors"]
        tables.append(table("Disponibilidad de sensores", ["Sensor", "Comunidad", "Tipo", "Estado", "Última comunicación UTC", "Última lectura UTC / valor"],
            [[r["code"] + " / " + r["name"], r.get("communityName"), r.get("type"), r["status"],
              r.get("lastCommunicationAt"), (f"{display(latest[r['id']]['measuredAt'])} / {latest[r['id']]['value']} {latest[r['id']]['unit']}"
                                            if r["id"] in latest else None)] for r in rows]))
        kpis = [("Sensores", len(rows)), ("Sin comunicación registrada", sum(not r.get("lastCommunicationAt") for r in rows))]
        notes.append("No se infiere conectividad actual a partir del estado administrativo. Última lectura en el conjunto consultado.")
    else:
        groups = defaultdict(list)
        for r in data["readings"]:
            groups[(r["variable"], r["unit"])].append(r["value"])
        stats = [[variable, unit, len(values), mean(values), min(values), max(values)]
                 for (variable, unit), values in sorted(groups.items())]
        for variable in ("Temperature", "RelativeHumidity", "WindSpeed", "RainfallLevel", "RiverOrReservoirLevel"):
            if not any(k[0] == variable for k in groups):
                stats.append([variable, None, 0, None, None, None])
        tables.append(table("Indicadores climáticos observados", ["Variable", "Unidad", "Muestras", "Promedio", "Mínimo", "Máximo"], stats))
        tables.append(table("Distribución de alertas", ["Nivel", "Cantidad"], sorted(Counter(r["level"] for r in data["alerts"]).items())))
        kpis = [("Lecturas analizadas", len(data["readings"])), ("Alertas", len(data["alerts"])), ("Lluvia acumulada", "N/D")]
        notes.append("Promedio aritmético por variable y unidad, sin ponderar por tiempo. La lluvia registrada es un nivel; no se suma como precipitación acumulada. No mezclar unidades.")
        if kind == ReportType.executive:
            kpis = [("Comunidades", len(data["communities"])), ("Sensores", len(data["sensors"])),
                    ("Alertas abiertas en período", sum(r["status"] == "Open" for r in data["alerts"])),
                    ("Eventos abiertos en período", sum(r["status"] == "Open" for r in data["events"]))]
            tables.insert(0, table("Situación general del sistema", ["Indicador", "Resultado"],
                [["Sensores activos", sum(r["status"] == "Active" for r in data["sensors"])],
                 ["Sensores inactivos", sum(r["status"] == "Inactive" for r in data["sensors"])],
                 ["Alertas en el período", len(data["alerts"])], ["Eventos en el período", len(data["events"])],
                 ["Lluvia acumulada", None]]))
            notes.append("Inventario de comunidades y sensores al momento de consulta; el rango temporal aplica a lecturas, alertas y eventos.")
    return {"title": TITLES[kind], "user": user["name"], "generated": datetime.now(timezone.utc).strftime("%d/%m/%Y %H:%M UTC"),
            "filters": {k: display(v) for k, v in request.filters.model_dump(mode="json", exclude_none=True).items()},
            "kpis": kpis, "tables": tables, "notes": notes, "correlation": correlation}
