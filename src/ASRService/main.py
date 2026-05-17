"""
ASR (Automatic Speech Recognition) Microservice
Powered by OpenAI Whisper (Arabic fine-tuned model)

Endpoints:
  POST /transcribe   — accepts audio file, returns Arabic transcription
  GET  /health       — liveness probe
"""

from __future__ import annotations

import io
import logging
from typing import Annotated

import torch
import whisper
from fastapi import FastAPI, File, HTTPException, UploadFile
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# ── Model Loading ────────────────────────────────────────────────────────────
# Uses "large-v3" by default; swap to "medium" for lighter deployments.
MODEL_NAME = "large-v3"
DEVICE = "cuda" if torch.cuda.is_available() else "cpu"

logger.info("Loading Whisper model '%s' on %s …", MODEL_NAME, DEVICE)
model = whisper.load_model(MODEL_NAME, device=DEVICE)
logger.info("Whisper model ready.")

# ── FastAPI App ───────────────────────────────────────────────────────────────
app = FastAPI(
    title="Tarteel ASR Service",
    description="Arabic speech recognition microservice using Whisper.",
    version="1.0.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # restrict in production
    allow_methods=["*"],
    allow_headers=["*"],
)


# ── Schemas ───────────────────────────────────────────────────────────────────
class TranscriptionResponse(BaseModel):
    text: str
    confidence: float
    language: str


class HealthResponse(BaseModel):
    status: str
    model: str
    device: str


# ── Routes ────────────────────────────────────────────────────────────────────
@app.get("/health", response_model=HealthResponse, tags=["ops"])
async def health() -> HealthResponse:
    return HealthResponse(status="ok", model=MODEL_NAME, device=DEVICE)


@app.post("/transcribe", response_model=TranscriptionResponse, tags=["asr"])
async def transcribe(
    audio: Annotated[UploadFile, File(description="Audio file (WAV / MP3 / OGG)")],
) -> TranscriptionResponse:
    """
    Transcribe an Arabic audio chunk.

    - Accepts WAV, MP3, OGG, FLAC (anything ffmpeg can decode).
    - Forces Arabic language to avoid mis-detection.
    - Returns the Arabic text and an average log-probability as confidence.
    """
    if not audio.content_type or not audio.content_type.startswith(("audio/", "application/octet")):
        raise HTTPException(status_code=415, detail="Unsupported media type.")

    audio_bytes = await audio.read()
    if len(audio_bytes) == 0:
        raise HTTPException(status_code=400, detail="Empty audio file.")

    # Write to an in-memory buffer so Whisper can decode it via ffmpeg
    audio_buffer = io.BytesIO(audio_bytes)
    audio_buffer.name = audio.filename or "chunk.wav"

    try:
        result = model.transcribe(
            audio_buffer,  # type: ignore[arg-type]
            language="ar",
            task="transcribe",
            fp16=(DEVICE == "cuda"),
        )
    except Exception as exc:
        logger.exception("Transcription failed")
        raise HTTPException(status_code=500, detail=str(exc)) from exc

    text = result.get("text", "").strip()

    # Average log-probability across segments → confidence in [0, 1]
    segments = result.get("segments", [])
    avg_log_prob = (
        sum(s.get("avg_logprob", -1.0) for s in segments) / len(segments)
        if segments
        else -1.0
    )
    confidence = max(0.0, min(1.0, (avg_log_prob + 1.0)))  # rough normalisation

    return TranscriptionResponse(
        text=text,
        confidence=round(confidence, 4),
        language="ar",
    )
