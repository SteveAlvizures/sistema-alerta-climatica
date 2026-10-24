"""Own audit persistence; identity comes from Auth HTTP, never another service database."""
import hashlib
import hmac
import asyncio
from contextlib import asynccontextmanager
from datetime import datetime, timezone
from typing import Annotated
from uuid import UUID, uuid4

import httpx
import jwt
from fastapi import Depends, FastAPI, Header, HTTPException, Query, Request
from fastapi.responses import JSONResponse
from fastapi.exceptions import RequestValidationError
from pydantic import BaseModel, Field, SecretStr
from pydantic_settings import BaseSettings, SettingsConfigDict
from pymongo import MongoClient
from pymongo.errors import PyMongoError


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore", hide_input_in_errors=True)
    jwt_key: SecretStr = Field(min_length=24)
    jwt_issuer: str = "climate-alert-api"
    jwt_audience: str = "climate-alert-client"
    auth_internal_key: SecretStr = Field(min_length=32)
    auth_service_url: str = "http://auth-service:3000"
    audit_mongo_host: str = "audit-mongodb"
    audit_mongo_database: str = "climate_audit"
    audit_mongo_user: str = "audit_app"
    audit_mongo_password: SecretStr
    legacy_audit_url: str = ""


class Identity(BaseModel):
    id: UUID
    name: str = Field(max_length=150)
    username: str = Field(max_length=254)


class AuditEvent(BaseModel):
    # Unknown identity fields from Auth are ignored; no hashes, JWTs or request bodies are persisted.
    id: UUID
    actor: Identity
    target: Identity
    action: str = Field(min_length=1, max_length=80)
    description: str = Field(min_length=1, max_length=1000)
    affectedRecordId: UUID
    occurredAt: datetime
    correlationId: UUID | None = None
    result: str = Field(default="Success", max_length=40)
    resource: str = Field(default="User", max_length=80)


def create_app(settings=None, transport=None):
    settings = settings or Settings()

    @asynccontextmanager
    async def lifespan(app):
        mongo = MongoClient(settings.audit_mongo_host, username=settings.audit_mongo_user,
            password=settings.audit_mongo_password.get_secret_value(), authSource=settings.audit_mongo_database,
            serverSelectionTimeoutMS=2000, connectTimeoutMS=2000, socketTimeoutMS=3000, tz_aware=True)
        app.state.mongo = mongo
        app.state.rows = mongo[settings.audit_mongo_database].audit_actions
        try:
            app.state.rows.create_index([("occurredAt", -1), ("_id", 1)])
            app.state.rows.create_index("username")
            app.state.rows.create_index("action")
        except PyMongoError:
            pass
        async with httpx.AsyncClient(timeout=3, transport=transport, trust_env=False, follow_redirects=False) as client:
            app.state.http = client
            async def synchronize_legacy():
                # Checkpoint and deduplication are owned by Audit. No password/JWT is persisted.
                checkpoints = mongo[settings.audit_mongo_database].sync_state
                while True:
                    try:
                        checkpoint = await asyncio.to_thread(checkpoints.find_one, {"_id": "legacy"}) or {}
                        params = {key: checkpoint[key] for key in ("after", "afterId") if key in checkpoint}
                        response = await client.get(settings.legacy_audit_url, params=params,
                            headers={"X-Internal-Service-Key": settings.auth_internal_key.get_secret_value()})
                        response.raise_for_status()
                        records = response.json()
                        for row in records:
                            document = {"_id": row["id"], "occurredAt": datetime.fromisoformat(row["occurredAt"].replace("Z", "+00:00")),
                                "username": row["username"], "action": row["action"], "affectedEntity": row["affectedEntity"],
                                "affectedRecordId": row["affectedRecordId"], "description": row["description"],
                                "identity": {"id": row["userId"], "name": row["username"]}, "result": "Success", "correlationId": None}
                            await asyncio.to_thread(app.state.rows.update_one, {"_id": document["_id"]}, {"$setOnInsert": document}, upsert=True)
                            await asyncio.to_thread(checkpoints.update_one, {"_id": "legacy"},
                                {"$set": {"after": row["occurredAt"], "afterId": row["id"]}}, upsert=True)
                        await asyncio.sleep(0.1 if records else 3)
                    except asyncio.CancelledError:
                        raise
                    except Exception:
                        await asyncio.sleep(3)
            task = asyncio.create_task(synchronize_legacy()) if settings.legacy_audit_url else None
            try:
                yield
            finally:
                if task:
                    task.cancel()
                    await asyncio.gather(task, return_exceptions=True)
        mongo.close()

    app = FastAPI(lifespan=lifespan, docs_url=None, redoc_url=None, openapi_url=None)

    @app.middleware("http")
    async def errors(request, next):
        try:
            correlation = str(UUID(request.headers.get("X-Correlation-ID", "")))
        except ValueError:
            correlation = str(uuid4())
        request.state.correlation = correlation
        try:
            response = await next(request)
        except PyMongoError:
            response = JSONResponse({"status": 503, "detail": "Audit unavailable.", "correlationId": correlation}, status_code=503)
        except Exception:
            response = JSONResponse({"status": 500, "detail": "Operation could not be completed.", "correlationId": correlation}, status_code=500)
        response.headers["X-Correlation-ID"] = correlation
        response.headers["Cache-Control"] = "no-store"
        return response

    @app.exception_handler(RequestValidationError)
    async def invalid(request, error):
        return JSONResponse({"status": 400, "detail": "Revisa los identificadores, fechas y filtros.",
            "correlationId": request.state.correlation}, status_code=400)

    async def administrator(request: Request):
        bearer = request.headers.get("Authorization", "")
        try:
            if not bearer.startswith("Bearer "):
                raise ValueError()
            claims = jwt.decode(bearer[7:], hashlib.sha256(settings.jwt_key.get_secret_value().encode()).digest(),
                algorithms=["HS256"], issuer=settings.jwt_issuer, audience=settings.jwt_audience,
                options={"require": ["sub", "exp", "iss", "aud"]})
            subject = str(UUID(claims["sub"]))
        except (jwt.PyJWTError, ValueError, KeyError, TypeError):
            raise HTTPException(401, "Invalid session") from None
        try:
            current = await request.app.state.http.get(settings.auth_service_url.rstrip("/") + "/internal/identity/current",
                headers={"Authorization": bearer, "X-Internal-Service-Key": settings.auth_internal_key.get_secret_value()})
            if current.status_code == 401:
                raise HTTPException(401, "Inactive session")
            current.raise_for_status()
            identity = current.json()
            if identity["id"] != subject or identity["isActive"] is not True:
                raise HTTPException(401, "Inactive session")
            if identity["role"] != "Administrator":
                raise HTTPException(403, "Administrator required")
        except (httpx.HTTPError, ValueError, KeyError, TypeError):
            raise HTTPException(503, "Identity unavailable") from None

    @app.get("/health")
    @app.get("/health/live")
    def live():
        return {"status": "Healthy"}

    @app.get("/health/ready")
    async def ready(request: Request):
        try:
            request.app.state.mongo[settings.audit_mongo_database].command("ping")
            response = await request.app.state.http.get(settings.auth_service_url.rstrip("/") + "/health/ready")
            response.raise_for_status()
            return {"status": "Healthy"}
        except (PyMongoError, httpx.HTTPError):
            raise HTTPException(503, "Not ready") from None

    @app.post("/internal/auth-events", status_code=204)
    def receive(event: AuditEvent, request: Request, key: Annotated[str, Header(alias="X-Internal-Service-Key")] = ""):
        if not hmac.compare_digest(key, settings.auth_internal_key.get_secret_value()):
            raise HTTPException(401, "Service credential required")
        if event.occurredAt.tzinfo is None:
            raise HTTPException(400, "Timezone required")
        document = {"_id": str(event.id), "occurredAt": event.occurredAt.astimezone(timezone.utc),
            "username": event.actor.username, "action": event.action, "affectedEntity": event.resource,
            "affectedRecordId": str(event.affectedRecordId), "description": event.description,
            "identity": event.actor.model_dump(mode="json"), "result": event.result,
            "correlationId": str(event.correlationId or request.state.correlation)}
        # Stable event ID makes outbox redelivery idempotent, including lost acknowledgements.
        request.app.state.rows.update_one({"_id": document["_id"]}, {"$setOnInsert": document}, upsert=True)

    @app.get("/api/audit-actions", dependencies=[Depends(administrator)])
    def history(request: Request, page: int = Query(1, ge=1, le=100000), pageSize: int = Query(20, ge=1, le=100),
        username: str | None = None, user: str | None = None, action: str | None = None, entity: str | None = None,
        dateFrom: datetime | None = None, dateTo: datetime | None = None,
        from_: datetime | None = Query(None, alias="from"), to: datetime | None = None):
        start, end = from_ or dateFrom, to or dateTo
        if any(value and value.tzinfo is None for value in [start, end]) or (start and end and start > end):
            raise HTTPException(400, "Invalid date range")
        query = {}
        for key, value in [("username", user or username), ("action", action), ("affectedEntity", entity)]:
            if value:
                import re
                query[key] = {"$regex": re.escape(value), "$options": "i"}
        if start or end:
            query["occurredAt"] = {**({"$gte": start} if start else {}), **({"$lte": end} if end else {})}
        rows = request.app.state.rows
        total = rows.count_documents(query)
        data = [{("id" if k == "_id" else k): v for k, v in row.items()}
            for row in rows.find(query).sort([("occurredAt", -1), ("_id", 1)]).skip((page-1)*pageSize).limit(pageSize)]
        return {"data": data, "pageIndex": page, "pageSize": pageSize, "totalCount": total,
            "totalPages": (total+pageSize-1)//pageSize, "hasNext": page*pageSize < total, "hasPrevious": page > 1}

    return app
