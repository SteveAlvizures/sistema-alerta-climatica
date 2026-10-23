from datetime import datetime, timedelta, timezone
from uuid import uuid4

from gridfs import GridFSBucket
from pymongo import ASCENDING, DESCENDING, MongoClient

from app.config import Settings
from app.errors import ReportError


def now():
    return datetime.now(timezone.utc)


class ReportStore:
    def __init__(self, settings: Settings):
        self.client = MongoClient(settings.report_mongo_host, settings.report_mongo_port,
            username=settings.report_mongo_user, password=settings.report_mongo_password.get_secret_value(),
            authSource=settings.report_mongo_database, serverSelectionTimeoutMS=2000,
            connectTimeoutMS=2000, socketTimeoutMS=3000, tz_aware=True)
        self.db = self.client[settings.report_mongo_database]
        self.reports = self.db.reports
        self.files = GridFSBucket(self.db, bucket_name="pdfs")

    def initialize(self):
        self.db.command("ping")
        for key in ("user.id", "type", "requestedAt", "state"):
            self.reports.create_index([(key, ASCENDING), ("_id", ASCENDING)], name=key.replace(".", "_") + "_idx")
        self.reports.create_index([("user.id", ASCENDING), ("requestedAt", DESCENDING), ("_id", ASCENDING)])
        self.reports.create_index([("state", ASCENDING), ("requestedAt", ASCENDING)])

    def create(self, kind, filters, user, correlation):
        item = {"_id": str(uuid4()), "type": kind, "filters": filters, "user": user, "requestedAt": now(),
                "state": "Pending", "filename": None, "generatedAt": None, "downloadedAt": None,
                "size": None, "error": None, "correlationId": correlation}
        self.reports.insert_one(item)
        return item

    def owned(self, report_id, user_id):
        self.recover_abandoned()
        item = self.reports.find_one({"_id": str(report_id), "user.id": user_id})
        if not item:
            raise ReportError(404, "Reporte no encontrado.")
        return item

    def recover_abandoned(self):
        # Abandoned synchronous operations expire without marking another replica's live operation failed.
        self.reports.update_many({"state": {"$in": ["Pending", "Processing"]},
            "requestedAt": {"$lt": now() - timedelta(minutes=2)}},
            {"$set": {"state": "Failed", "error": "La generación fue interrumpida. Solicita un nuevo reporte."}})

    @staticmethod
    def public(item):
        return {("id" if key == "_id" else key): value for key, value in item.items() if key != "fileId"}

    def complete(self, item, pdf):
        filename = f"vigia-rural-{item['type']}-{item['_id']}.pdf"
        file_id = self.files.upload_from_stream(filename, pdf, metadata={"reportId": item["_id"]})
        try:
            self.reports.update_one({"_id": item["_id"]}, {"$set": {"state": "Completed", "fileId": file_id,
                "filename": filename, "generatedAt": now(), "size": len(pdf)}})
        except Exception:
            self.files.delete(file_id)
            raise
