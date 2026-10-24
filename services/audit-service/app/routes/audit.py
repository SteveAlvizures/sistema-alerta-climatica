from datetime import datetime
from typing import Any

from fastapi import APIRouter, Depends, HTTPException, Query, status
from pymongo.database import Database

from app.core.database import get_database
from app.core.security import require_permission
from app.models.audit import AuditEventCreate, AuditEventResponse
from app.services.audit_service import AuditService


router = APIRouter(
    prefix="/api/audit-events",
    tags=["Audit"],
)


def get_service(
    database: Database = Depends(get_database),
) -> AuditService:
    return AuditService(database)


@router.post(
    "",
    response_model=AuditEventResponse,
    status_code=status.HTTP_201_CREATED,
)
def create_event(
    event: AuditEventCreate,
    current_user: dict[str, Any] = Depends(
        require_permission("audit.write")
    ),
    service: AuditService = Depends(get_service),
):
    return service.create_event(
        event,
        current_user,
    )


@router.get("")
def get_events(
    page: int = Query(default=1, ge=1),
    page_size: int = Query(
        default=20,
        alias="pageSize",
        ge=1,
        le=100,
    ),
    action: str | None = None,
    entity_type: str | None = Query(
        default=None,
        alias="entityType",
    ),
    entity_id: str | None = Query(
        default=None,
        alias="entityId",
    ),
    user_id: str | None = Query(
        default=None,
        alias="userId",
    ),
    date_from: datetime | None = Query(
        default=None,
        alias="dateFrom",
    ),
    date_to: datetime | None = Query(
        default=None,
        alias="dateTo",
    ),
    _: dict[str, Any] = Depends(
        require_permission("audit.read")
    ),
    service: AuditService = Depends(get_service),
):
    try:
        return service.get_events(
            page=page,
            page_size=page_size,
            action=action,
            entity_type=entity_type,
            entity_id=entity_id,
            user_id=user_id,
            date_from=date_from,
            date_to=date_to,
        )
    except ValueError as exc:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=str(exc),
        ) from exc


@router.get(
    "/{event_id}",
    response_model=AuditEventResponse,
)
def get_event_by_id(
    event_id: str,
    _: dict[str, Any] = Depends(
        require_permission("audit.read")
    ),
    service: AuditService = Depends(get_service),
):
    event = service.get_event_by_id(
        event_id
    )

    if event is None:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Evento de auditoría no encontrado.",
        )

    return event
