FROM python:3.13-slim-bookworm
WORKDIR /service
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt && useradd --uid 10001 --create-home audit
COPY app.py .
COPY tests tests
USER audit
ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1
ENTRYPOINT ["uvicorn","app:create_app","--factory","--host","0.0.0.0","--port","8000","--no-access-log","--no-server-header"]
