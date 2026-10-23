FROM python:3.13-slim-bookworm AS base
ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1 PYTHONPATH=/service
RUN apt-get update && apt-get install -y --no-install-recommends libpango-1.0-0 libpangoft2-1.0-0 libharfbuzz-subset0 fonts-dejavu-core && rm -rf /var/lib/apt/lists/*
WORKDIR /service
COPY pyproject.toml requirements.lock ./
RUN pip install --no-cache-dir -r requirements.lock
COPY app ./app
RUN pip install --no-cache-dir --no-deps . && useradd --uid 10001 --create-home report
FROM base AS test
USER root
RUN pip install --no-cache-dir '.[test]' && apt-get update && apt-get install -y --no-install-recommends poppler-utils && rm -rf /var/lib/apt/lists/*
COPY tests ./tests
CMD ["pytest", "-q"]
FROM base AS runtime
USER report
EXPOSE 8000
CMD ["uvicorn", "app.main:create_app", "--factory", "--host", "0.0.0.0", "--port", "8000", "--no-access-log", "--no-server-header", "--log-level", "warning"]
