import asyncio
import logging
import time
from contextlib import asynccontextmanager
from typing import Annotated, Literal
from uuid import UUID, uuid4

import httpx
from fastapi import Depends, FastAPI, Query, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse, Response
from gridfs.errors import NoFile
from pymongo.errors import PyMongoError

from app.application import collect, context_for
from app.auth import identity
from app.config import Settings
from app.errors import ReportError
from app.models import ReportRequest, ReportType, STATES, TITLES
from app.persistence import ReportStore, now
from app.rendering import render

logger = logging.getLogger("report-service")
logger.setLevel(logging.INFO)
logger.propagate = False
if not logger.handlers:
    logger.addHandler(logging.StreamHandler())


def create_app(settings: Settings | None = None, transport=None):
    settings = settings or Settings()

    @asynccontextmanager
    async def lifespan(app):
        app.state.settings = settings
        app.state.store = ReportStore(settings)
        app.state.busy = False
        # Start alive even if persistence is unavailable. Readiness and requests fail closed.
        try:
            await asyncio.to_thread(app.state.store.initialize)
        except PyMongoError:
            pass
        async with httpx.AsyncClient(timeout=settings.report_http_timeout_seconds, transport=transport,
                                     follow_redirects=False, trust_env=False) as http:
            app.state.http = http
            yield
        app.state.store.client.close()

    app = FastAPI(title="Vigía Rural Report Service", lifespan=lifespan, docs_url=None,
                  redoc_url=None, openapi_url=None)

    def problem(request, status, detail):
        return JSONResponse({"type": "about:blank", "title": "No se pudo completar la solicitud",
            "status": status, "detail": detail, "correlationId": request.state.correlation_id},
            status_code=status, media_type="application/problem+json")

    @app.middleware("http")
    async def correlation(request, call_next):
        started = time.monotonic()
        try:
            correlation_id = str(UUID(request.headers.get("X-Correlation-ID", "")))
        except ValueError:
            correlation_id = str(uuid4())
        request.state.correlation_id = correlation_id
        try:
            response = await call_next(request)
        except Exception:
            response = problem(request, 500, "No se pudo completar la operación.")
        response.headers["X-Correlation-ID"] = correlation_id
        response.headers["Cache-Control"] = "no-store"
        logger.info("request correlation=%s method=%s status=%s duration_ms=%s", correlation_id,
                    request.method, response.status_code, round((time.monotonic() - started) * 1000))
        return response

    @app.exception_handler(ReportError)
    async def controlled(request, error):
        return problem(request, error.status, error.detail)

    @app.exception_handler(RequestValidationError)
    async def invalid(request, error):
        return problem(request, 400, "Revisa el tipo, los identificadores, las fechas y los filtros enviados.")

    @app.exception_handler(PyMongoError)
    async def unavailable(request, error):
        return problem(request, 503, "El almacenamiento de reportes no está disponible.")

    @app.get("/health")
    @app.get("/health/live")
    async def live():
        return {"status": "Healthy"}

    @app.get("/health/ready")
    async def ready():
        try:
            await asyncio.to_thread(app.state.store.initialize)
            response = await app.state.http.get(settings.auth_service_url.rstrip("/") + "/health/ready")
            response.raise_for_status()
            return {"status": "Healthy"}
        except (PyMongoError, httpx.HTTPError):
            return JSONResponse({"status": "Unhealthy"}, status_code=503)

    User = Annotated[dict, Depends(identity)]

    @app.get("/api/reports/types")
    async def types(user: User):
        return [{"id": kind, "name": title, "states": STATES.get(kind, []),
                 "supportsDates": kind not in ("communities", "sensors", "sensor-status"),
                 "supportsSensor": kind != "communities", "supportsVariable": kind not in ("communities", "events")}
                for kind, title in TITLES.items()]

    @app.post("/api/reports", status_code=201)
    async def generate(body: ReportRequest, request: Request, user: User):
        # Capacity is per process, not a distributed queue; persistent states expose interrupted work.
        if app.state.busy:
            raise ReportError(429, "Hay un reporte en generación. Intenta nuevamente en unos segundos.")
        app.state.busy = True
        store = app.state.store
        item = None
        try:
            item = await asyncio.to_thread(store.create, body.type, body.filters.model_dump(mode="json", exclude_none=True),
                                           user, request.state.correlation_id)
            await asyncio.to_thread(store.reports.update_one, {"_id": item["_id"]}, {"$set": {"state": "Processing"}})
            try:
                async with asyncio.timeout(settings.report_deadline_seconds):
                    # Budget covers sources and rendering; Mongo operations have independent driver timeouts.
                    data = await collect(body, settings, app.state.http, {"Authorization": request.headers["Authorization"],
                                                                         "X-Correlation-ID": request.state.correlation_id})
                    try:
                        context = context_for(body, data, user, request.state.correlation_id)
                    except (KeyError, TypeError, ValueError):
                        raise ReportError(502, "La fuente de datos devolvió un contrato incompatible.") from None
                    pdf = await render(context,
                                       settings.report_deadline_seconds, settings.report_max_pdf_bytes)
                await asyncio.to_thread(store.complete, item, pdf)
            except TimeoutError:
                raise ReportError(504, "La generación excedió el tiempo permitido. Reduce el rango o los filtros.") from None
            return store.public(await asyncio.to_thread(store.owned, item["_id"], user["id"]))
        except Exception as error:
            if item:
                # A failed final metadata read must not invalidate an already stored, downloadable PDF.
                detail = error.detail if isinstance(error, ReportError) else "No se pudo generar el documento."
                try:
                    await asyncio.to_thread(store.reports.update_one,
                                            {"_id": item["_id"], "state": {"$in": ["Pending", "Processing"]}},
                                            {"$set": {"state": "Failed", "error": detail}})
                except PyMongoError:
                    pass
            raise
        finally:
            app.state.busy = False

    @app.get("/api/reports")
    async def history(user: User, page: int = Query(1, ge=1, le=10000), pageSize: int = Query(10, ge=1, le=50),
                      type: ReportType | None = None,
                      state: Literal["Pending", "Processing", "Completed", "Failed"] | None = None):
        query = {"user.id": user["id"]}
        if type:
            query["type"] = type
        if state:
            query["state"] = state

        def read():
            store = app.state.store
            store.recover_abandoned()
            total = store.reports.count_documents(query)
            rows = list(store.reports.find(query).sort([("requestedAt", -1), ("_id", 1)]).skip((page-1)*pageSize).limit(pageSize))
            return {"data": [store.public(r) for r in rows], "totalCount": total, "pageIndex": page,
                    "pageSize": pageSize, "totalPages": (total + pageSize - 1)//pageSize,
                    "hasNext": page*pageSize < total, "hasPrevious": page > 1}
        return await asyncio.to_thread(read)

    @app.get("/api/reports/{report_id}")
    @app.get("/api/reports/{report_id}/status")
    async def metadata(report_id: UUID, user: User):
        return app.state.store.public(await asyncio.to_thread(app.state.store.owned, report_id, user["id"]))

    @app.get("/api/reports/{report_id}/download")
    async def download(report_id: UUID, user: User):
        store = app.state.store
        item = await asyncio.to_thread(store.owned, report_id, user["id"])
        if item["state"] != "Completed":
            raise ReportError(409, "El reporte todavía no está disponible para descargar.")
        if not item.get("fileId") or not item.get("filename") or not item.get("size"):
            raise ReportError(409, "El documento no está disponible. Genera un nuevo reporte.")

        def read_pdf():
            # Existing PDFs depend on ownership/Auth and Mongo only, not on the original data sources.
            try:
                with store.files.open_download_stream(item["fileId"]) as stream:
                    if stream.length != item["size"] or stream.length > settings.report_max_pdf_bytes:
                        raise ReportError(409, "El documento no está disponible. Genera un nuevo reporte.")
                    pdf = stream.read()
                if not pdf.startswith(b"%PDF-"):
                    raise ReportError(409, "El documento no está disponible. Genera un nuevo reporte.")
                store.reports.update_one({"_id": str(report_id)}, {"$set": {"downloadedAt": now()}})
                return pdf
            except NoFile:
                raise ReportError(404, "El PDF no está disponible. Genera un nuevo reporte.") from None
        pdf = await asyncio.to_thread(read_pdf)
        return Response(pdf, media_type="application/pdf", headers={
            "Content-Disposition": f'attachment; filename="{item["filename"]}"', "X-Content-Type-Options": "nosniff"})

    return app
