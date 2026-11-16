"""Use the installed Blender MCP add-on's local JSON/TCP bridge."""
import argparse
import json
import pathlib
import socket

parser = argparse.ArgumentParser()
parser.add_argument('file', nargs='?')
parser.add_argument('--code')
args = parser.parse_args()
code = args.code if args.code else pathlib.Path(args.file).read_text(encoding='utf-8-sig')
with socket.create_connection(('127.0.0.1', 9876), timeout=5) as connection:
    connection.settimeout(55)
    connection.sendall(json.dumps({'type': 'execute_code', 'params': {'code': code}}).encode('utf-8'))
    data = b''
    while True:
        chunk = connection.recv(65536)
        if not chunk:
            raise RuntimeError('Blender disconnected before responding')
        data += chunk
        try:
            response = json.loads(data.decode('utf-8'))
            break
        except (json.JSONDecodeError, UnicodeDecodeError):
            continue
    print(json.dumps(response, ensure_ascii=False))
    if response.get('status') != 'success':
        raise SystemExit(1)
