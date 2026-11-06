import re

RAIN = {0: "Seco", 1: "Mojado leve", 2: "Muy mojado"}

def categorical(row):
    return row.get("variable") == "RainfallLevel" and row.get("unit") == "estado" or row.get("sensorCode") == "ESP32-RAIN-01"

def number(value):
    return f"{value:.1f}".rstrip("0").rstrip(".")

def reading_value(row, field="value"):
    value = row.get(field)
    if value is None:
        return "N/D"
    if categorical(row):
        return RAIN.get(value, "Estado desconocido")
    return f"{number(value)} {'°C' if row.get('unit') == 'C' else row.get('unit', '')}".strip()

def boundary(value, lower=True):
    rounded = round(value)
    strict = abs(value-rounded-(.000001 if lower else -.000001)) < 1e-9
    return f"{('>' if lower else '<') if strict else ('≥' if lower else '≤')} {number(rounded if strict else value)}"

def threshold(row):
    if categorical(row):
        return RAIN.get(row.get("activationPoint"), "Estado configurado")
    if row.get("usesRange"):
        minimum, maximum = row.get("minValue"), row.get("maxValue")
        parts = ([boundary(minimum)] if minimum is not None else []) + ([boundary(maximum, False)] if maximum is not None else [])
        text = " y ".join(parts)
    else:
        value = row.get("activationPoint")
        if value is None: return "N/D"
        op = row.get("comparisonOperator") or "≥"
        text = boundary(value) if op == ">=" else boundary(value, False) if op == "<=" else f"{op} {number(value)}"
    return f"{text} {'°C' if row.get('unit') == 'C' else row.get('unit', '')}".strip()

def event_title(row, fallback):
    hint = (row.get("sensorCode") or "") + " " + (row.get("description") or "")
    critical = row.get("level") == "Red"
    if re.search(r"DHT11-TEMP|SF13-TEMP", hint): return "Temperatura crítica" if critical else "Temperatura elevada"
    if re.search(r"DHT11-HUM|SF13-HUM", hint): return "Humedad crítica" if critical else "Humedad elevada"
    if re.search(r"WATER-LEVEL|SF13-LEVEL", hint): return "Nivel de agua crítico" if critical else "Nivel de agua elevado"
    if re.search(r"ESP32-RAIN|SF13-RAIN", hint): return "Superficie muy mojada" if row.get("value") == 2 or row.get("level") == "Orange" else "Superficie mojada"
    return fallback
