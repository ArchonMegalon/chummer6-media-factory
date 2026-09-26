"""Local private worker liveness check; no provider call or new image request."""
import argparse
import http.client
import json
from pathlib import Path
import socket

from origin_scene_worker import load_token


def check(socket_path: Path, token_file: Path) -> dict:
    connection = http.client.HTTPConnection("origin-scene-worker", timeout=3)
    try:
        connection.sock = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
        connection.sock.settimeout(3)
        connection.sock.connect(str(socket_path))
        connection.request("POST", "/v1/health", b"{}", {
            "Authorization": "Bearer " + load_token(token_file), "Content-Type": "application/json"})
        response = connection.getresponse()
        body = response.read(4097)
        if response.status != 200 or len(body) > 4096:
            raise ValueError("Private worker unavailable.")
        value = json.loads(body)
        if (value.get("schema") != "chummer.media.origin-scene-worker/v1"
                or value.get("accountErasureSupported") is not True
                or type(value.get("dispatchEnabled")) is not bool):
            raise ValueError("Unexpected private worker health.")
        return value
    finally:
        connection.close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--socket", type=Path, required=True)
    parser.add_argument("--token-file", type=Path, required=True)
    args = parser.parse_args()
    try:
        print(json.dumps(check(args.socket, args.token_file)))
    except Exception:
        raise SystemExit("Private worker health check failed.")
