"""Render non-secret manifests with explicit images; no modifications to source manifests."""
from pathlib import Path
import argparse,re,subprocess,sys
p=argparse.ArgumentParser();p.add_argument('--tag',required=True);p.add_argument('--output',required=True);args=p.parse_args()
if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_.-]{0,127}',args.tag) or args.tag=='REPLACE_WITH_COMMIT':p.error('Use a specific release tag')
directory=Path(__file__).resolve().parent
result=subprocess.run(['kubectl','kustomize',str(directory)],capture_output=True,text=True)
if result.returncode:sys.exit(result.stderr)
output=result.stdout.replace(':REPLACE_WITH_COMMIT',':'+args.tag)
assert ':REPLACE_WITH_COMMIT' not in output
Path(args.output).write_text(output,encoding='utf-8')
print('Rendered non-secret release manifests:',args.output)
