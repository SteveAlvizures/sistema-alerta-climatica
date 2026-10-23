import asyncio
import json
from typing import Any

import httpx

from app.errors import ReportError

Row = dict[str, Any]


class PagedClient:
    """Read owner APIs over HTTP; Report has no credentials for their databases.

    Bounded pages and total checks reject oversized or incomplete exports instead
    of silently publishing a partial PDF. They do not provide a cross-service snapshot.
    """
    path = ""

    def __init__(self, http: httpx.AsyncClient, base_url: str, limit: int):
        self.http, self.url, self.limit = http, base_url.rstrip("/") + self.path, limit

    async def fetch(self, filters: dict, headers: dict) -> list[Row]:
        result: list[Row] = []
        for page in range(1, self.limit // 50 + 2):
            for attempt in range(2):
                try:
                    # Streaming bounds bytes before decoding; a broken source cannot return an unlimited body.
                    async with self.http.stream("GET", self.url, params={**filters, "page": page,
                                                                        "pageSize": 50}, headers=headers) as response:
                        if response.status_code in (502, 503, 504) and attempt == 0:
                            await asyncio.sleep(0.1)
                            continue
                        response.raise_for_status()
                        body = bytearray()
                        async for chunk in response.aiter_bytes():
                            body.extend(chunk)
                            if len(body) > 2_000_000:
                                raise ReportError(502, "La fuente devolvió una respuesta demasiado grande.")
                        data = json.loads(body)
                    rows = data["data"]
                    if (not isinstance(rows, list) or len(rows) > 50
                            or not all(isinstance(row, dict) for row in rows)
                            or type(data["totalCount"]) is not int or data["totalCount"] < 0
                            or type(data["hasNext"]) is not bool):
                        raise ValueError()
                    if data["totalCount"] > self.limit or len(result) + len(rows) > self.limit:
                        raise ReportError(422, "El reporte supera el límite de registros. Reduce los filtros.")
                    result.extend(rows)
                    if not data["hasNext"]:
                        if len(result) != data["totalCount"]:
                            raise ValueError()
                        return result
                    if not rows:
                        raise ValueError()
                    break
                except httpx.TimeoutException:
                    if attempt == 0:
                        continue
                    raise ReportError(504, "La fuente de datos no respondió a tiempo.") from None
                except (httpx.HTTPError, ValueError, KeyError, TypeError):
                    raise ReportError(502, "No se pudo obtener un conjunto completo de datos.") from None
        raise ReportError(422, "El reporte supera el límite de páginas. Reduce los filtros.")


class CommunityClient(PagedClient):
    path = "/api/communities"


class SensorClient(PagedClient):
    path = "/api/sensors"


class ReadingClient(PagedClient):
    path = "/api/sensor-readings"

    async def latest(self, filters: dict, headers: dict) -> list[Row]:
        client = PagedClient(self.http, self.url + "/latest", self.limit)
        return await client.fetch(filters, headers)


class AlertClient(PagedClient):
    path = "/api/alerts"


class EventClient(PagedClient):
    path = "/api/events"
