from fastapi import FastAPI

from app.core.config import settings
from app.core.database import check_database_connection
from app.routes.audit import router as audit_router


app = FastAPI(
    title="Vigía Rural Audit Service",
    version="1.0.0",
)


@app.get("/health")
def health():
    database_ok = check_database_connection()

    return {
        "status": "ok" if database_ok else "degraded",
        "service": "audit-service",
        "database": "connected" if database_ok else "disconnected",
    }


app.include_router(audit_router)
