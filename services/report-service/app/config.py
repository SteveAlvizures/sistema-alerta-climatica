from pydantic import Field, SecretStr
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore", hide_input_in_errors=True)
    jwt_key: SecretStr = Field(min_length=24)
    jwt_issuer: str = "climate-alert-api"
    jwt_audience: str = "climate-alert-client"
    auth_internal_key: SecretStr = Field(min_length=32)
    auth_service_url: str = "http://localhost:3000"
    report_mongo_host: str = "localhost"
    report_mongo_port: int = 27017
    report_mongo_database: str = "climate_reports"
    report_mongo_user: str = "report_app"
    report_mongo_password: SecretStr
    community_api_url: str = "http://localhost:5152"
    sensor_api_url: str = "http://localhost:5152"
    reading_api_url: str = "http://localhost:5152"
    alert_api_url: str = "http://localhost:5152"
    event_api_url: str = "http://localhost:5152"
    report_max_rows: int = Field(default=2000, ge=50, le=10000)
    report_max_pdf_bytes: int = Field(default=10_000_000, ge=10000, le=50_000_000)
    report_deadline_seconds: int = Field(default=22, ge=5, le=25)
    report_http_timeout_seconds: float = Field(default=3, ge=0.1, le=5)
