from bson.errors import InvalidId
from fastapi import APIRouter, Depends, HTTPException, Query
from fastapi.responses import Response
from pymongo.database import Database

from app.core.database import get_database
from app.core.security import require_permission
from app.services.report_service import ReportService


router = APIRouter(
    prefix="/api/reports",
    tags=["Reports"],
)


def get_service(
    database: Database = Depends(get_database),
):
    return ReportService(database)


@router.post("/generate", status_code=201)
async def generate_report(
    communityId: str,
    dateFrom: str | None = None,
    dateTo: str | None = None,
    auth=Depends(require_permission("reports.generate")),
    service: ReportService = Depends(get_service),
):
    payload = auth["payload"]

    generated_by = (
        payload.get("email")
        or payload.get("username")
        or payload.get("sub")
        or "unknown"
    )

    try:
        return await service.generate(
            communityId,
            auth["token"],
            generated_by,
            dateFrom,
            dateTo,
        )

    except ValueError as exc:
        raise HTTPException(
            status_code=404,
            detail=str(exc),
        )


@router.get("")
def list_reports(
    auth=Depends(require_permission("reports.read")),
    service: ReportService = Depends(get_service),
):
    return service.list_reports()


@router.get("/{report_id}")
def get_report(
    report_id: str,
    auth=Depends(require_permission("reports.read")),
    service: ReportService = Depends(get_service),
):
    try:
        report = service.get_metadata(report_id)
    except InvalidId:
        report = None

    if report is None:
        raise HTTPException(
            status_code=404,
            detail="Reporte no encontrado.",
        )

    return report


@router.get("/{report_id}/pdf")
def get_report_pdf(
    report_id: str,
    auth=Depends(require_permission("reports.read")),
    service: ReportService = Depends(get_service),
):
    try:
        pdf = service.get_pdf(report_id)
    except InvalidId:
        pdf = None

    if pdf is None:
        raise HTTPException(
            status_code=404,
            detail="Reporte no encontrado.",
        )

    return Response(
        content=pdf,
        media_type="application/pdf",
        headers={
            "Content-Disposition":
                f'inline; filename="reporte-{report_id}.pdf"'
        },
    )
