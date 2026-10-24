from datetime import datetime, timezone
from pathlib import Path

from bson import Binary, ObjectId
from jinja2 import Environment, FileSystemLoader, select_autoescape
from pymongo.database import Database
from weasyprint import HTML

from app.services.service_client import ServiceClient


templates_path = Path(__file__).resolve().parent.parent / "templates"

jinja = Environment(
    loader=FileSystemLoader(str(templates_path)),
    autoescape=select_autoescape(["html", "xml"]),
)


class ReportService:

    def __init__(self, database: Database):
        self.collection = database["reports"]
        self.client = ServiceClient()

    async def generate(
        self,
        community_id: str,
        token: str,
        generated_by: str,
        date_from: str | None,
        date_to: str | None,
    ):
        community = await self.client.get_community(
            community_id,
            token,
        )

        if community is None:
            raise ValueError("La comunidad indicada no existe.")

        readings = await self.client.get_readings(
            community_id,
            token,
            date_from,
            date_to,
        )

        alerts = await self.client.get_alerts(
            community_id,
            token,
        )

        reading_items = (
            readings.get("data")
            or readings.get("items")
            or readings
            or []
        )

        if isinstance(reading_items, dict):
            reading_items = []

        alert_items = alerts.get("data", [])

        generated_at = datetime.now(timezone.utc)

        template = jinja.get_template("community_report.html")

        html = template.render(
            community=community,
            readings=reading_items,
            alerts=alert_items,
            generated_at=generated_at,
            date_from=date_from,
            date_to=date_to,
        )

        pdf_bytes = HTML(string=html).write_pdf()

        document = {
            "type": "community-summary",
            "communityId": community_id,
            "communityName": community.get("name"),
            "generatedBy": generated_by,
            "generatedAt": generated_at,
            "dateFrom": date_from,
            "dateTo": date_to,
            "readingCount": len(reading_items),
            "alertCount": len(alert_items),
            "contentType": "application/pdf",
            "pdf": Binary(pdf_bytes),
        }

        result = self.collection.insert_one(document)

        return self.to_metadata({
            **document,
            "_id": result.inserted_id,
        })

    def list_reports(self):
        documents = self.collection.find(
            {},
            {"pdf": 0},
        ).sort("generatedAt", -1)

        return [
            self.to_metadata(document)
            for document in documents
        ]

    def get_metadata(self, report_id: str):
        document = self.collection.find_one(
            {"_id": ObjectId(report_id)},
            {"pdf": 0},
        )

        if not document:
            return None

        return self.to_metadata(document)

    def get_pdf(self, report_id: str):
        document = self.collection.find_one(
            {"_id": ObjectId(report_id)}
        )

        if not document:
            return None

        return bytes(document["pdf"])

    @staticmethod
    def to_metadata(document):
        return {
            "id": str(document["_id"]),
            "type": document["type"],
            "communityId": document["communityId"],
            "communityName": document["communityName"],
            "generatedBy": document["generatedBy"],
            "generatedAt": document["generatedAt"],
            "dateFrom": document.get("dateFrom"),
            "dateTo": document.get("dateTo"),
            "readingCount": document["readingCount"],
            "alertCount": document["alertCount"],
            "contentType": document["contentType"],
        }
