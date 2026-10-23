from datetime import datetime
from enum import StrEnum
from typing import Literal
from uuid import UUID

from pydantic import BaseModel, ConfigDict, model_validator


class ReportType(StrEnum):
    communities = "communities"
    sensors = "sensors"
    readings = "readings"
    alerts = "alerts"
    events = "events"
    sensor_status = "sensor-status"
    statistics = "statistics"
    executive = "executive"


TITLES = dict(zip(ReportType, ["Comunidades", "Sensores", "Lecturas", "Alertas", "Historial de eventos",
                              "Estado de sensores", "Estadísticas climáticas", "Resumen ejecutivo"]))
VARIABLES = ["Temperature", "RelativeHumidity", "WindSpeed", "RainfallLevel", "RiverOrReservoirLevel",
             "SmokeConcentration", "OtherEnvironmental"]
STATES = {"communities": ["Active", "Inactive"], "sensors": ["Active", "Inactive"],
          "sensor-status": ["Active", "Inactive"], "readings": ["Active", "Inactive"],
          "alerts": ["Open", "Acknowledged", "Closed"], "events": ["Open", "Closed"]}


class Filters(BaseModel):
    model_config = ConfigDict(extra="forbid")
    communityId: UUID | None = None
    sensorId: UUID | None = None
    dateFrom: datetime | None = None
    dateTo: datetime | None = None
    variable: Literal["Temperature", "RelativeHumidity", "WindSpeed", "RainfallLevel",
                      "RiverOrReservoirLevel", "SmokeConcentration", "OtherEnvironmental"] | None = None
    status: Literal["Active", "Inactive", "Open", "Acknowledged", "Closed"] | None = None

    @model_validator(mode="after")
    def dates(self):
        for value in (self.dateFrom, self.dateTo):
            if value and value.tzinfo is None:
                raise ValueError("Las fechas deben incluir zona horaria.")
        if self.dateFrom and self.dateTo and self.dateFrom > self.dateTo:
            raise ValueError("La fecha desde no puede superar la fecha hasta.")
        return self


class ReportRequest(BaseModel):
    model_config = ConfigDict(extra="forbid")
    type: ReportType
    filters: Filters = Filters()

    @model_validator(mode="after")
    def supported(self):
        f = self.filters
        if f.status and f.status not in STATES.get(self.type, []):
            raise ValueError("Estado incompatible con el reporte.")
        if self.type == ReportType.communities and (f.sensorId or f.variable):
            raise ValueError("Filtros incompatibles con comunidades.")
        if self.type in (ReportType.communities, ReportType.sensors, ReportType.sensor_status) and (
                f.dateFrom or f.dateTo):
            raise ValueError("El inventario actual no admite filtros de fecha.")
        if self.type == ReportType.events and f.variable:
            raise ValueError("Los eventos no admiten filtro de variable.")
        return self
