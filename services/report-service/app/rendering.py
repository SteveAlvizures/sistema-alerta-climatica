"""Run PDF rendering in a killable child process; never pass credentials to it."""
import asyncio
import json
import sys
from pathlib import Path

from jinja2 import Environment, FileSystemLoader, select_autoescape

from app.errors import ReportError


async def render(context: dict, timeout: float, max_bytes: int) -> bytes:
    process = await asyncio.create_subprocess_exec(sys.executable, "-m", "app.rendering",
        stdin=asyncio.subprocess.PIPE, stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.DEVNULL)
    try:
        output, _ = await asyncio.wait_for(process.communicate(json.dumps(context).encode()), timeout)
        if process.returncode or not output.startswith(b"%PDF-"):
            raise ReportError(500, "No se pudo generar el documento.")
        if len(output) > max_bytes:
            raise ReportError(422, "El PDF supera el tamaño permitido. Reduce los filtros.")
        return output
    except (TimeoutError, asyncio.CancelledError):
        if process.returncode is None:
            process.kill()
        await process.wait()
        raise


def denied_fetcher(url, **kwargs):
    raise ValueError("External resources are disabled")


if __name__ == "__main__":
    from weasyprint import HTML
    context = json.load(sys.stdin)
    environment = Environment(loader=FileSystemLoader(Path(__file__).parent / "templates"),
                              autoescape=select_autoescape(["html"]))
    html = environment.get_template("report.html").render(**context)
    sys.stdout.buffer.write(HTML(string=html, url_fetcher=denied_fetcher).write_pdf())
