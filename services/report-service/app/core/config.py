import os
from urllib.parse import quote_plus

from dotenv import load_dotenv

load_dotenv()


class Settings:
    report_port: int = int(os.getenv("REPORT_PORT", "3005"))

    mongo_host: str = os.getenv("MONGO_HOST", "127.0.0.1")
    mongo_port: int = int(os.getenv("MONGO_PORT", "27019"))
    mongo_db: str = os.getenv("MONGO_DB", "report_db")
    mongo_user: str = os.getenv("MONGO_USER", "report_admin")
    mongo_password: str = os.getenv("MONGO_PASSWORD", "")

    jwt_secret: str = os.getenv("JWT_SECRET", "")
    jwt_issuer: str = os.getenv("JWT_ISSUER", "vigiarural-auth")
    jwt_audience: str = os.getenv("JWT_AUDIENCE", "vigiarural")

    community_service_url: str = os.getenv(
        "COMMUNITY_SERVICE_URL",
        "http://127.0.0.1:3002",
    )

    sensor_service_url: str = os.getenv(
        "SENSOR_SERVICE_URL",
        "http://127.0.0.1:3004",
    )

    alert_service_url: str = os.getenv(
        "ALERT_SERVICE_URL",
        "http://127.0.0.1:3003",
    )

    @property
    def mongo_uri(self) -> str:
        user = quote_plus(self.mongo_user)
        password = quote_plus(self.mongo_password)

        return (
            f"mongodb://{user}:{password}"
            f"@{self.mongo_host}:{self.mongo_port}/"
            "?authSource=admin"
        )


settings = Settings()
