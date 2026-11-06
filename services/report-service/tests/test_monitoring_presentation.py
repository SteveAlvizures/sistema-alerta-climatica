import pytest
from app.monitoring_presentation import reading_value, threshold, event_title

@pytest.mark.parametrize("value,label", [(0,"Seco"),(1,"Mojado leve"),(2,"Muy mojado")])
def test_rain_is_categorical_in_readings_and_alerts(value,label):
    row = {"variable":"RainfallLevel","unit":"estado","value":value,"detectedValue":value,"activationPoint":value}
    assert reading_value(row) == label
    assert reading_value(row,"detectedValue") == label
    assert threshold(row) == label

def test_units_and_strict_boundaries_are_readable_without_changing_semantics():
    assert reading_value({"value":28,"unit":"C"}) == "28 °C"
    assert threshold({"activationPoint":34.000001,"comparisonOperator":">=","unit":"°C"}) == "> 34 °C"
    assert threshold({"usesRange":True,"minValue":30.000001,"maxValue":50,"unit":"%"}) == "> 30 y ≤ 50 %"

def test_contextual_titles_preserve_legacy_fallback():
    assert event_title({"description":"Evento asociado con DEMO-SF13-LEVEL-RED", "level":"Red"},"Inundación") == "Nivel de agua crítico"
    assert event_title({"description":"Legacy event", "level":"Red"},"Inundación") == "Inundación"

def test_exported_pdf_uses_real_rain_states_including_zero():
    import asyncio
    import io
    from pypdf import PdfReader
    from app.application import context_for
    from app.models import ReportRequest
    from app.rendering import render
    rows = [{"sensorId":"rain", "sensorCode":"ESP32-RAIN-01", "sensorName":"Superficie de lluvia",
             "communityName":"Santa Fe Zona 13", "measuredAt":"2026-11-05T12:00:00Z",
             "variable":"RainfallLevel", "value":value, "unit":"estado"} for value in (0,1,2)]
    context = context_for(ReportRequest(type="readings"), {"readings":rows}, {"name":"Prueba"}, "test")
    pdf = asyncio.run(render(context,20,10_000_000))
    text = "".join(page.extract_text() for page in PdfReader(io.BytesIO(pdf)).pages)
    assert all(label in text for label in ("Seco","Mojado leve","Muy mojado"))
    assert "0 estado" not in text
