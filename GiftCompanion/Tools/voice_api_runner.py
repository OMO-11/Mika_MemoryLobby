"""Run the delivered API unchanged and record import evidence for conservative trimming."""
import json
import os
from pathlib import Path
import runpy
import sys
import threading
import time


def record_dependencies():
    output = os.environ.get('GIFT_VOICE_DEPENDENCY_REPORT')
    if not output:
        return
    root = Path.cwd().resolve()
    previous = None
    while True:
        time.sleep(5)
        modules = {}
        for name, module in list(sys.modules.items()):
            filename = getattr(module, '__file__', None)
            if not filename:
                continue
            try:
                path = Path(filename).resolve()
                modules[name] = path.relative_to(root).as_posix() if path.is_relative_to(root) else str(path)
            except (OSError, ValueError):
                continue
        if modules == previous:
            continue
        previous = modules
        data = {'scope': 'Observed Python modules; absence alone does not prove a dependency removable.',
                'executable': sys.executable, 'modules': modules}
        Path(output).write_text(json.dumps(data, indent=2, ensure_ascii=False) + '\n', 'utf-8')


threading.Thread(target=record_dependencies, daemon=True).start()
runpy.run_path('api_v2.py', run_name='__main__')
