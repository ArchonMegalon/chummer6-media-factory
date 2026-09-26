# Local keyless image; runtime secrets and retained images are file mounts only.
FROM python:3.12-alpine@sha256:d09d15e60962ca365d1cd544a48773bac9d33f2fb1b00f2aa0deec78ade7dc31
RUN pip install --no-cache-dir --only-binary=:all: Pillow==12.1.1
WORKDIR /app
COPY --chmod=0444 scripts/origin_scene_worker.py scripts/origin_scene_store.py scripts/origin_scene_health.py scripts/render_guide_asset.py /app/
ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1 CHUMMER_MEDIA_FACTORY_STATE_DIR=/private/render
USER 1000:1000
STOPSIGNAL SIGTERM
ENTRYPOINT ["python3", "/app/origin_scene_worker.py"]
CMD ["--socket", "/private/socket/worker.sock", "--token-file", "/private/auth/worker.token", "--database", "/private/data/media.sqlite", "--private-render-directory", "/private/render"]
