"""Owned local inference host. The delivered GPT-SoVITS API and models stay unchanged."""
import asyncio
import ctypes
import hmac
import json
import os
import runpy
import sys
import threading
import traceback
from pathlib import Path

instance = os.environ.pop("GIFT_VOICE_INSTANCE", "")
parent_pid = int(os.environ.pop("GIFT_VOICE_PARENT_PID", "0"))
status_file = os.environ.pop("GIFT_VOICE_STATUS_FILE", "")
if len(instance) != 32 or parent_pid <= 0:
    raise RuntimeError("Voice host requires an owning application")


def watch_parent():
    # A killed Unity process cannot run its shutdown callback.
    if os.name == "nt":
        kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel.OpenProcess.restype = ctypes.c_void_p
        kernel.OpenProcess.argtypes = [ctypes.c_uint32, ctypes.c_int, ctypes.c_uint32]
        kernel.WaitForSingleObject.argtypes = [ctypes.c_void_p, ctypes.c_uint32]
        kernel.CloseHandle.argtypes = [ctypes.c_void_p]
        handle = kernel.OpenProcess(0x00100000, False, parent_pid)
        if not handle:
            os._exit(2)
        kernel.WaitForSingleObject(handle, 0xFFFFFFFF)
        kernel.CloseHandle(handle)
        os._exit(0)


threading.Thread(target=watch_parent, daemon=True).start()
sys.path.insert(0, os.getcwd())


def diagnostic(state, error=None):
    if not status_file:
        return
    frames = traceback.extract_tb(error.__traceback__) if error else []
    Path(status_file).write_text(json.dumps({"state": state, "errorType": type(error).__name__ if error else None,
        "source": Path(frames[-1].filename).name if frames else None, "line": frames[-1].lineno if frames else None}), "utf-8")


try:
    diagnostic("loading")
    api = runpy.run_path("api_v2.py", run_name="gift_voice_api")
except Exception as error:
    diagnostic("failed", error)
    raise
from fastapi import Request
from fastapi.responses import JSONResponse
import uvicorn

app = api["APP"]
gate = asyncio.Lock()
busy = False
server = None


@app.middleware("http")
async def owned_requests(request, call_next):
    if not request.url.path.startswith("/gift/") or not hmac.compare_digest(request.headers.get("X-Gift-Instance", ""), instance):
        return JSONResponse({"error": "owned_service"}, status_code=403)
    return await call_next(request)


@app.get("/gift/health")
async def health():
    return {"version": 1, "instance": instance, "pid": os.getpid(), "ready": True, "busy": busy}


@app.post("/gift/tts")
async def synthesize(request: Request):
    global busy
    payload = await request.json()
    if not isinstance(payload.get("text"), str) or len(payload["text"]) > 500:
        return JSONResponse({"error": "invalid_segment"}, status_code=400)
    payload = api["TTS_Request"](**payload).dict()
    payload.update(streaming_mode=0, media_type="wav", return_fragment=False)
    async with gate:
        busy = True
        # Keep health responsive while the synchronous Torch model works.
        future = asyncio.get_running_loop().run_in_executor(None, lambda: asyncio.run(api["tts_handle"](payload)))
        try:
            return await asyncio.shield(future)
        except asyncio.CancelledError:
            # Client disconnect does not free the model for a second request.
            await future
            raise
        finally:
            busy = False


@app.post("/gift/shutdown")
async def shutdown():
    if server is not None:
        server.should_exit = True
    return {"stopping": True}


server = uvicorn.Server(uvicorn.Config(app, host="127.0.0.1", port=api["port"], log_level="warning", access_log=False))
diagnostic("ready")
server.run()
