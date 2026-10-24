from datetime import datetime
from typing import Any

from bson import ObjectId
from pymongo.database import Database

from app.models.audit import (
    AuditEventCreate,
    AuditEventResponse,
    utc_now,
)


class AuditService:
    def __init__(self, database: Database):
        self.collection = database["audit_events"]

    def create_event(
        self,
        event: AuditEventCreate,
        current_user: dict[str, Any],
    ) -> AuditEventResponse:
        document = {
            "user_id": str(
                current_user.get("sub")
                or current_user.get("userId")
                or ""
            )
            or None,
            "username": current_user.get("username")
            or current_user.get("email"),
            "action": event.action,
            "entity_type": event.entity_type,
            "entity_id": event.entity_id,
            "description": event.description,
            "metadata": event.metadata,
            "created_at": utc_now(),
        }

        result = self.collection.insert_one(document)

        return self._map(document, result.inserted_id)

    def get_events(
        self,
        page: int,
        page_size: int,
        action: str | None = None,
        entity_type: str | None = None,
        entity_id: str | None = None,
        user_id: str | None = None,
        date_from: datetime | None = None,
        date_to: datetime | None = None,
    ) -> dict[str, Any]:
        if page < 1:
            raise ValueError(
                "La página debe ser mayor o igual a 1."
            )

        if page_size < 1 or page_size > 100:
            raise ValueError(
                "El tamaño de página debe estar entre 1 y 100."
            )

        if (
            date_from is not None
            and date_to is not None
            and date_from > date_to
        ):
            raise ValueError(
                "La fecha inicial no puede ser mayor que la fecha final."
            )

        query: dict[str, Any] = {}

        if action:
            query["action"] = action

        if entity_type:
            query["entity_type"] = entity_type

        if entity_id:
            query["entity_id"] = entity_id

        if user_id:
            query["user_id"] = user_id

        if date_from or date_to:
            query["created_at"] = {}

            if date_from:
                query["created_at"]["$gte"] = date_from

            if date_to:
                query["created_at"]["$lte"] = date_to

        total = self.collection.count_documents(query)

        cursor = (
            self.collection
            .find(query)
            .sort("created_at", -1)
            .skip((page - 1) * page_size)
            .limit(page_size)
        )

        data = [
            self._map(document, document["_id"])
            for document in cursor
        ]

        total_pages = (
            0
            if total == 0
            else (total + page_size - 1) // page_size
        )

        return {
            "data": data,
            "pageIndex": page,
            "pageSize": page_size,
            "totalPages": total_pages,
            "totalCount": total,
            "hasPrevious": page > 1,
            "hasNext": page < total_pages,
        }

    def get_event_by_id(
        self,
        event_id: str,
    ) -> AuditEventResponse | None:
        if not ObjectId.is_valid(event_id):
            return None

        document = self.collection.find_one(
            {"_id": ObjectId(event_id)}
        )

        if document is None:
            return None

        return self._map(
            document,
            document["_id"],
        )

    @staticmethod
    def _map(
        document: dict[str, Any],
        object_id: ObjectId,
    ) -> AuditEventResponse:
        return AuditEventResponse(
            id=str(object_id),
            user_id=document.get("user_id"),
            username=document.get("username"),
            action=document["action"],
            entity_type=document["entity_type"],
            entity_id=document.get("entity_id"),
            description=document.get("description"),
            metadata=document.get("metadata", {}),
            created_at=document["created_at"],
        )
