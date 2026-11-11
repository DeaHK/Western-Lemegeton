import re,json,difflib,collections,zipfile,xml.etree.ElementTree as ET
from compare_projects import ROOTS, OUT
rows=json.loads((OUT/'differences.json').read_text(encoding='utf-8'))
indexes=json.loads((OUT/'inventory.json').read_text(encoding='utf-8'))
maps=[]
for root,idx in zip(ROOTS,indexes):
    ids={}
    for p in idx:
        if p.startswith('Assets/') and p.endswith('.meta'):
            m=re.search(r'^guid: (\w+)',(root/p).read_text(encoding='utf-8-sig'),re.M)
            if m: ids[m[1]]=p[:-5]
    maps.append(ids)
def resolve(s,side):
    return re.sub(r'guid: (\w+)',lambda m:'guid: '+maps[side].get(m[1],m[1]),s)
out=[]
for r in rows:
    p=r['path']
    if p.endswith('.prefab') or p.endswith('.asset') and p.startswith('Assets/'):
        for side,root in enumerate(ROOTS):
            if not (root/p).exists():continue
            t=(root/p).read_text(encoding='utf-8-sig')
            out.append('\n## '+str(root/p))
            for block in re.split(r'(?=^--- !u!)',t,flags=re.M):
                if not block.startswith('--- !u!114 '):continue
                lines=block.splitlines()
                start=next((i for i,l in enumerate(lines) if 'm_EditorClassIdentifier:' in l),len(lines))
                out.append(resolve('\n'.join(lines[start:]),side))
    if p.endswith('.docx'):
        side=0 if r['status']=='only-main' else 1
        with zipfile.ZipFile(ROOTS[side]/p) as z:
            t=ET.fromstring(z.read('word/document.xml'))
            out.append(p+'\n'+''.join(t.itertext()))
(OUT/'prefab-settings.txt').write_text('\n'.join(out),encoding='utf-8')
for p in ['Assets/Scenes/Main.unity','Assets/Wilderness Room Catalog.asset']:
    texts=[resolve((root/p).read_text(encoding='utf-8-sig'),side).splitlines(True) for side,root in enumerate(ROOTS)]
    (OUT/(p.split('/')[-1]+'.resolved.diff')).write_text(''.join(difflib.unified_diff(*texts,n=2)),encoding='utf-8')
pa='Assets/Scripts/Enemy.cs'; pb='Assets/Scripts/Enemy/Enemy.cs'
(OUT/'Enemy-moved.diff').write_text(''.join(difflib.unified_diff((ROOTS[0]/pa).read_text(encoding='utf-8-sig').splitlines(True),(ROOTS[1]/pb).read_text(encoding='utf-8-sig').splitlines(True),fromfile=pa,tofile=pb)),encoding='utf-8')
print('Counts',len(rows),collections.Counter(r['status'] for r in rows))
print('Unstable/errors',[(side,p,v) for side,idx in enumerate(indexes) for p,v in idx.items() if v.get('unstable') or 'error' in v])
print('Same GUID moved',[(g,maps[0][g],maps[1][g]) for g in maps[0].keys() & maps[1].keys() if maps[0][g]!=maps[1][g]])
print('Main only files', [r['path'] for r in rows if r['status']=='only-main' and not r['path'].endswith('.meta') and r['path'].split('/')[0] in ('Assets','Docs')])
