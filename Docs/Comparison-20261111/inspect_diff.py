import json, sys, difflib
from compare_projects import ROOTS, OUT
rows=json.loads((OUT/'differences.json').read_text(encoding='utf-8'))
for r in rows:
    name=r['path']
    if r['status']!='changed' or not any(x in name for x in sys.argv[1:]): continue
    try:
        a,b=[(root/name).read_text(encoding='utf-8-sig').splitlines(True) for root in ROOTS]
    except UnicodeError: continue
    print(''.join(difflib.unified_diff(a,b,fromfile='main/'+name,tofile='testWL/'+name,n=2)))
