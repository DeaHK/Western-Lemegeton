import json,csv,hashlib
from compare_projects import ROOTS,OUT
indexes=json.loads((OUT/'inventory.json').read_text(encoding='utf-8'))
checked=0;changed=[]
for root,index in zip(ROOTS,indexes):
    for p,old in index.items():
        if p.split('/')[0] not in ('Assets','Docs','Packages','ProjectSettings','AGENTS.md','.gitignore','README.md'):continue
        with (root/p).open('rb') as f:current=hashlib.file_digest(f,'sha256').hexdigest()
        checked+=1
        if current!=old['sha256']:changed.append(str(root/p))
path=OUT/'file-differences-with-functions.csv'
with path.open(encoding='utf-8-sig',newline='') as f:
    reader=csv.DictReader(f);fields=reader.fieldnames;rows=list(reader)
for r in rows:
    if r['상대경로'].endswith(('EditableUIBaker.cs','GameUIView.cs')):
        r['기능/의미']+=' 주의: B Player에는 Q키 TryTag 무기전환이 실제로 남아 있어 리볼버 고정/전환미구현 UI와 불일치.'
    if r['상대경로']=='UserSettings/HierarchyWindow.settings':r['기능/의미']='B의 Hierarchy 창 개인 편집기 설정 파일. 게임 동작 추가 아님.'
    if r['상대경로']=='Assets/Scripts/RunBuild.cs':r['기능/의미']+=' 해당 스크립트 GUID의 Assets 내부 직렬화 참조도 검색되지 않음.'
with path.open('w',encoding='utf-8-sig',newline='') as f:
    w=csv.DictWriter(f,fieldnames=fields);w.writeheader();w.writerows(rows)
assert len(rows)==1730 and len({r['상대경로'] for r in rows})==1730
assert all(r['기능/의미'] for r in rows)
assert not list(OUT.glob('*.md'))
print('Original source/asset/config/doc files rechecked:',checked,'changed:',changed)
print('CSV:',len(rows),'unique rows; every row explained; no generated MD files')
