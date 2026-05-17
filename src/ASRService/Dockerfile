FROM python:3.12-slim

# System deps: ffmpeg for audio decoding
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Pre-download Whisper model at build time to avoid cold-start delays
ARG WHISPER_MODEL=large-v3
RUN python -c "import whisper; whisper.load_model('${WHISPER_MODEL}')"

COPY . .

EXPOSE 8000
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
