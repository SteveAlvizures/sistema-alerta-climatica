from fastapi import FastAPI

from app.core.database import check_database_connection
from app.routes.reports import router as reports_router


app = FastAPI(
    title="Vigía Rural Report Service",
    version="1.0.0",
)


@app.get("/health")
def health():
    database_ok = check_database_connection()

    return {
        "status": "ok" if database_ok else "degraded",
        "service": "report-service",
        "database": (
            "connected"
            if database_ok
            else "disconnected"
        ),
    }


app.include_router(reports_router)
