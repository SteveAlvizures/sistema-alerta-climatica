import os
from dotenv import load_dotenv

load_dotenv()


class Settings:
    audit_port: int = int(os.getenv("AUDIT_PORT", "3006"))

    mongo_host: str = os.getenv("MONGO_HOST", "127.0.0.1")
    mongo_port: int = int(os.getenv("MONGO_PORT", "27018"))
    mongo_db: str = os.getenv("MONGO_DB", "audit_db")
    mongo_user: str = os.getenv("MONGO_USER", "audit_admin")
    mongo_password: str = os.getenv("MONGO_PASSWORD", "")

    jwt_secret: str = os.getenv("JWT_SECRET", "")
    jwt_issuer: str = os.getenv("JWT_ISSUER", "vigiarural-auth")
    jwt_audience: str = os.getenv("JWT_AUDIENCE", "vigiarural")

    @property
    def mongo_uri(self) -> str:
        return (
            f"mongodb://{self.mongo_user}:{self.mongo_password}"
            f"@{self.mongo_host}:{self.mongo_port}/"
            f"?authSource=admin"
        )


settings = Settings()
