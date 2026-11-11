import os, json, hashlib, collections, csv, difflib
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

ROOTS = [Path('C:/22851054/Western-Lemegeton-main'), Path('C:/22851054/testWL')]
OUT = ROOTS[1] / 'Docs/Comparison-20261111'

def inventory(root):
    result = {}
    for parent, dirs, files in os.walk(root):
        dirs[:] = [d for d in dirs if d not in ('.git', 'Comparison-20261111')]
        for name in files:
            path = Path(parent) / name
            try:
                st = path.stat()
                result[path.relative_to(root).as_posix()] = {'size': st.st_size, 'mtime': st.st_mtime_ns}
            except OSError as exc:
                result[path.relative_to(root).as_posix()] = {'error': str(exc)}
    return result

if __name__ == '__main__':
    indexes = [inventory(root) for root in ROOTS]
    (OUT / 'inventory.json').write_text(json.dumps(indexes, ensure_ascii=False), encoding='utf-8')
    for root, index in zip(ROOTS, indexes):
        totals = collections.defaultdict(lambda: [0, 0])
        for name, data in index.items():
            top = name.split('/')[0]
            totals[top][0] += 1
            totals[top][1] += data.get('size', 0)
        print(root, json.dumps(totals))

    def digest(job):
        side, name = job
        path = ROOTS[side] / name
        try:
            with path.open('rb') as stream:
                value = hashlib.file_digest(stream, 'sha256').hexdigest()
            st = path.stat()
            return side, name, value, st.st_size != indexes[side][name].get('size') or st.st_mtime_ns != indexes[side][name].get('mtime')
        except OSError as exc:
            return side, name, 'ERROR: ' + str(exc), True

    with ThreadPoolExecutor(max_workers=4) as pool:
        for side, name, value, changed in pool.map(digest, ((s, n) for s in (0, 1) for n in indexes[s])):
            indexes[side][name]['sha256'] = value
            indexes[side][name]['unstable'] = changed
    (OUT / 'inventory.json').write_text(json.dumps(indexes, ensure_ascii=False), encoding='utf-8')
    rows = []
    for name in sorted(set(indexes[0]) | set(indexes[1])):
        a, b = (index.get(name, {}) for index in indexes)
        status = 'only-main' if not b else 'only-testWL' if not a else 'same' if a['sha256'] == b['sha256'] else 'changed'
        if status != 'same':
            rows.append({'path': name, 'status': status, 'main_size': a.get('size', ''), 'testWL_size': b.get('size', ''), 'unstable': a.get('unstable', False) or b.get('unstable', False)})
    with (OUT / 'all-file-differences.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.DictWriter(stream, fieldnames=['path','status','main_size','testWL_size','unstable'])
        writer.writeheader(); writer.writerows(rows)
    (OUT / 'differences.json').write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding='utf-8')
    primary = [row for row in rows if row['path'].split('/')[0] not in ('Library','Logs','Temp','UserSettings','Verification','Backups','Previews')]
    print('PRIMARY DIFFERENCES', json.dumps(primary, ensure_ascii=False))
    print('ALL DIFFERENCE COUNTS', collections.Counter((r['path'].split('/')[0], r['status']) for r in rows))
    diff_lines = []
    for row in primary:
        name = row['path']
        if name.endswith('.meta'): continue
        try:
            content = [(root / name).read_text(encoding='utf-8-sig').splitlines(keepends=True) if (root / name).is_file() else [] for root in ROOTS]
        except (UnicodeError, OSError): continue
        diff_lines.extend(difflib.unified_diff(*content, fromfile='main/' + name, tofile='testWL/' + name))
    (OUT / 'source-changes.diff').write_text(''.join(diff_lines), encoding='utf-8')
