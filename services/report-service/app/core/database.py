from pymongo import MongoClient
from pymongo.database import Database

from app.core.config import settings


client = MongoClient(
    settings.mongo_uri,
    serverSelectionTimeoutMS=3000,
)

database: Database = client[settings.mongo_db]


def get_database() -> Database:
    return database


def check_database_connection() -> bool:
    try:
        client.admin.command("ping")
        return True
    except Exception:
        return False
