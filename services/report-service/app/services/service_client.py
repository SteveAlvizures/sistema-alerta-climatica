from typing import Any

import httpx

from app.core.config import settings


class ServiceClient:

    @staticmethod
    async def _get(
        url: str,
        token: str,
        params: dict[str, Any] | None = None,
    ):
        headers = {
            "Authorization": f"Bearer {token}"
        }

        async with httpx.AsyncClient(timeout=15.0) as client:
            response = await client.get(
                url,
                headers=headers,
                params=params,
            )

            response.raise_for_status()

            return response.json()

    async def get_community(
        self,
        community_id: str,
        token: str,
    ):
        communities = await self._get(
            f"{settings.community_service_url}/api/communities",
            token,
        )

        for community in communities:
            if community.get("id") == community_id:
                return community

        return None

    async def get_readings(
        self,
        community_id: str,
        token: str,
        date_from: str | None,
        date_to: str | None,
    ):
        params = {
            "communityId": community_id,
            "page": 1,
            "pageSize": 100,
        }

        if date_from:
            params["dateFrom"] = date_from

        if date_to:
            params["dateTo"] = date_to

        return await self._get(
            f"{settings.sensor_service_url}/api/sensor-readings",
            token,
            params,
        )

    async def get_alerts(
        self,
        community_id: str,
        token: str,
    ):
        return await self._get(
            f"{settings.alert_service_url}/api/alerts",
            token,
            {
                "communityId": community_id,
                "page": 1,
                "pageSize": 100,
            },
        )
