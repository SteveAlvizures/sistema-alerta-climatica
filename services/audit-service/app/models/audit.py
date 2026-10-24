from datetime import datetime, timezone
from typing import Any

from pydantic import BaseModel, Field


class AuditEventCreate(BaseModel):
    action: str = Field(min_length=2, max_length=100)
    entity_type: str = Field(min_length=2, max_length=100)
    entity_id: str | None = Field(default=None, max_length=150)
    description: str | None = Field(default=None, max_length=1000)
    metadata: dict[str, Any] = Field(default_factory=dict)


class AuditEventResponse(BaseModel):
    id: str
    user_id: str | None
    username: str | None
    action: str
    entity_type: str
    entity_id: str | None
    description: str | None
    metadata: dict[str, Any]
    created_at: datetime


def utc_now() -> datetime:
    return datetime.now(timezone.utc)
