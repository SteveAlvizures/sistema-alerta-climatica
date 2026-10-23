"""Run PDF rendering in a killable child process; never pass credentials to it."""
import asyncio
import json
import sys
from pathlib import Path

from jinja2 import Environment, FileSystemLoader, select_autoescape

from app.errors import ReportError


async def render(context: dict, timeout: float, max_bytes: int) -> bytes:
    """Bound parent-process output and terminate the renderer on timeout/cancellation.

    WeasyPrint still builds the document in the child; row and container memory
    limits remain necessary. Jinja context contains report data, never credentials.
    """
    process = await asyncio.create_subprocess_exec(sys.executable, "-m", "app.rendering",
        stdin=asyncio.subprocess.PIPE, stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.DEVNULL)

    async def write_context():
        try:
            process.stdin.write(json.dumps(context).encode())
            await process.stdin.drain()
        except (BrokenPipeError, ConnectionResetError):
            pass  # The exit code below handles an early renderer failure.
        finally:
            process.stdin.close()

    async def read_pdf():
        output = bytearray()
        while chunk := await process.stdout.read(min(65536, max_bytes + 1 - len(output))):
            output.extend(chunk)
            if len(output) > max_bytes:
                raise ReportError(422, "El PDF supera el tamaño permitido. Reduce los filtros.")
        return bytes(output)

    writer = asyncio.create_task(write_context())
    try:
        async with asyncio.timeout(timeout):
            output = await read_pdf()
            await writer
            await process.wait()
        if process.returncode or not output.startswith(b"%PDF-"):
            raise ReportError(500, "No se pudo generar el documento.")
        return output
    finally:
        if process.returncode is None:
            process.kill()
        await process.wait()
        writer.cancel()
        await asyncio.gather(writer, return_exceptions=True)


def denied_fetcher(url, **kwargs):
    """Prevent template resources from reading local files or making network requests."""
    raise ValueError("External resources are disabled")


if __name__ == "__main__":
    from weasyprint import HTML
    context = json.load(sys.stdin)
    # Escaping keeps source text out of HTML markup; the closed fetcher also blocks resource-based file/network access.
    environment = Environment(loader=FileSystemLoader(Path(__file__).parent / "templates"),
                              autoescape=select_autoescape(["html"]))
    html = environment.get_template("report.html").render(**context)
    sys.stdout.buffer.write(HTML(string=html, url_fetcher=denied_fetcher).write_pdf())
