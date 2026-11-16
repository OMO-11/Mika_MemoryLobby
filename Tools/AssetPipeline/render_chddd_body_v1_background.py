"""Render saved body draft in background Blender without touching the live scene."""
import argparse, bpy, sys
from pathlib import Path

parser=argparse.ArgumentParser()
parser.add_argument('--names',required=True)
parser.add_argument('--motion',action='store_true')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
scene=bpy.context.scene
scene['body_v1_preview_batch']=args.names
scene['body_v1_preview_motion']=args.motion
path=Path('W:/Machine/VSCode/Mika_Gift/Tools/AssetPipeline/preview_chddd_body_v1.py')
exec(compile(path.read_text(encoding='utf-8'),str(path),'exec'))
