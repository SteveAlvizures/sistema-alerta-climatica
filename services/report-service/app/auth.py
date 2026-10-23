import hashlib
from uuid import UUID

import httpx
import jwt
from fastapi import Request

from app.errors import ReportError


async def identity(request: Request) -> dict:
    settings = request.app.state.settings
    bearer = request.headers.get("Authorization", "")
    if not bearer.startswith("Bearer "):
        raise ReportError(401, "Se requiere una sesión válida.")
    try:
        claims = jwt.decode(bearer[7:], hashlib.sha256(settings.jwt_key.get_secret_value().encode()).digest(),
                            algorithms=["HS256"], issuer=settings.jwt_issuer, audience=settings.jwt_audience,
                            options={"require": ["sub", "exp", "iss", "aud"]})
        user_id = str(UUID(claims["sub"]))
    except (jwt.PyJWTError, ValueError, TypeError, KeyError):
        raise ReportError(401, "Se requiere una sesión válida.") from None
    try:
        response = await request.app.state.http.get(settings.auth_service_url.rstrip("/") +
            "/internal/identity/current", headers={"Authorization": bearer,
            "X-Internal-Service-Key": settings.auth_internal_key.get_secret_value(),
            "X-Correlation-ID": request.state.correlation_id})
        if response.status_code == 401:
            raise ReportError(401, "La sesión ya no está activa.")
        response.raise_for_status()
        current = response.json()
        if current.get("id") != user_id or current.get("isActive") is not True:
            raise ReportError(401, "La sesión ya no está activa.")
        if current.get("role") not in ("Administrator", "Operator") or "OperateSystem" not in current.get(
                "permissions", []):
            raise ReportError(403, "Tu cuenta no tiene permiso para utilizar reportes.")
        return {"id": user_id, "name": current["name"]}
    except (httpx.HTTPError, ValueError, KeyError):
        raise ReportError(503, "No se pudo verificar la sesión. Intenta nuevamente.") from None
