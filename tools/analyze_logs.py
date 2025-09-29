import re
import sys
from collections import defaultdict, deque
from datetime import datetime

API_RE = re.compile(r"\[API\] CAM (\d+) t=(\d{2}:\d{2}:\d{2}\.\d{3}) frameSeq=(\d+) detSeq=(\d+) id=([^ ]+) xy=\(([^,]+),([^,]+),([^,]+),([^\)]+)\) lbl='([^']*)' s=([0-9.]+)")
DRAW_RE = re.compile(r"\[DRAW\] CAM (\d+) t=(\d{2}:\d{2}:\d{2}\.\d{3}) latestSeq=(\d+) detSeq=(\d+) src=(API|HANGOVER) hangover=(\d+) id=([^ ]+) xy=\(([^,]+),([^,]+),([^,]+),([^\)]+)\) inside=(True|False)")
SKIP_RE = re.compile(r"\[DRAW-SKIP\] CAM (\d+) t=(\d{2}:\d{2}:\d{2}\.\d{3}) reason=(LAG|TTL)[^\n]*detSeq=(\d+).*id=([^ ]+) ?.*hangover=(\d+)")
HANG_SUP_RE = re.compile(r"\[HANGOVER-SUPPRESS\] CAM (\d+) t=(\d{2}:\d{2}:\d{2}\.\d{3}) id=(\d+) noUpd=(\d+) .* lastSeq=(\d+) xy=\(([^,]+),([^,]+),([^,]+),([^\)]+)\)")

def parse_time(s: str) -> datetime:
    # Parses HH:mm:ss.fff to a datetime anchored to today (date unused for diffs)
    today = datetime.now().date()
    h=int(s[0:2]); m=int(s[3:5]); sec=int(s[6:8]); ms=int(s[9:12])
    return datetime(today.year, today.month, today.day, h, m, sec, ms*1000)

def near_equal_box(a, b, eps=1e-4):
    return all(abs(float(x)-float(y)) <= eps for x,y in zip(a,b))

def main(path):
    api = defaultdict(list)      # cam -> list of {t, detSeq, id, box}
    draw = defaultdict(list)     # cam -> list of {t, detSeq, src, hang, id, box, inside}
    skip = defaultdict(list)
    hangsup = defaultdict(list)

    with open(path, 'r', encoding='utf-8', errors='ignore') as f:
        for line in f:
            m = API_RE.search(line)
            if m:
                cam = int(m.group(1)); t=parse_time(m.group(2)); detSeq=int(m.group(4))
                idv = m.group(5); box=(m.group(6),m.group(7),m.group(8),m.group(9))
                api[cam].append(dict(t=t, detSeq=detSeq, id=idv, box=box, raw=line.strip()))
                continue
            m = DRAW_RE.search(line)
            if m:
                cam = int(m.group(1)); t=parse_time(m.group(2)); detSeq=int(m.group(4))
                src=m.group(5); hang=int(m.group(6)); idv=m.group(7)
                box=(m.group(8),m.group(9),m.group(10),m.group(11)); inside=(m.group(12)=='True')
                draw[cam].append(dict(t=t, detSeq=detSeq, src=src, hang=hang, id=idv, box=box, inside=inside, raw=line.strip()))
                continue
            m = SKIP_RE.search(line)
            if m:
                cam=int(m.group(1)); t=parse_time(m.group(2)); reason=m.group(3)
                detSeq=int(m.group(4)); idv=m.group(5); hang=int(m.group(6))
                skip[cam].append(dict(t=t, reason=reason, detSeq=detSeq, id=idv, hang=hang, raw=line.strip()))
                continue
            m = HANG_SUP_RE.search(line)
            if m:
                cam=int(m.group(1)); t=parse_time(m.group(2)); idv=m.group(3)
                noUpd=int(m.group(4)); lastSeq=int(m.group(5))
                box=(m.group(6),m.group(7),m.group(8),m.group(9))
                hangsup[cam].append(dict(t=t, id=idv, noUpd=noUpd, lastSeq=lastSeq, box=box, raw=line.strip()))
                continue

    # Analysis per camera
    reports = []
    for cam in sorted(set(api.keys()) | set(draw.keys())):
        a = api.get(cam, [])
        d = draw.get(cam, [])

        # Build last API per (track id) timeline
        last_api_by_id = {}
        stagnant_api = []  # [(id, count, first_t, last_t, box)]
        seq_by_id = defaultdict(list)
        for item in a:
            seq_by_id[item['id']].append(item)
        for tid, items in seq_by_id.items():
            cnt_same = 0
            last_box = None
            first_t = None
            for it in items:
                box = it['box']
                if last_box is not None and near_equal_box(box, last_box):
                    cnt_same += 1
                else:
                    if cnt_same >= 5:  # long repeats
                        stagnant_api.append((tid, cnt_same+1, first_t, items_prev_t, last_box))
                    cnt_same = 0
                    first_t = it['t']
                last_box = box
                items_prev_t = it['t']
            if last_box is not None and cnt_same >= 5:
                stagnant_api.append((tid, cnt_same+1, first_t, items_prev_t, last_box))

        # Find draws without recent API: HANGOVER too long or repeated same coords
        ghost_draws = []
        # track last API detSeq per id for ref
        last_api_seq_by_id = {}
        for it in a:
            last_api_seq_by_id[it['id']] = it['detSeq']

        # sliding window to detect repeated draws with identical boxes when src=HANGOVER
        window_by_id = defaultdict(lambda: deque(maxlen=6))
        for it in d:
            tid = it['id']
            window_by_id[tid].append(it)
            if it['src'] == 'HANGOVER' and it['hang'] > 3:
                # if last few have identical boxes, flag
                w = window_by_id[tid]
                if len(w) >= 4:
                    boxes = [x['box'] for x in w][-4:]
                    if all(near_equal_box(boxes[i], boxes[0]) for i in range(1,4)):
                        ghost_draws.append((cam, tid, it['t'], it['detSeq'], it['hang'], it['box']))

        # Compare counts roughly
        reports.append({
            'cam': cam,
            'api_count': len(a),
            'draw_count': len(d),
            'stagnant_api': stagnant_api,
            'ghost_draws': ghost_draws,
        })

    # Print summary
    for r in reports:
        print(f"CAM {r['cam']}: API={r['api_count']} DRAW={r['draw_count']}")
        if r['stagnant_api']:
            print(f"  Suspected API-stuck coords (>=6 repeats): {len(r['stagnant_api'])}")
            for (tid, cnt, t0, t1, box) in r['stagnant_api'][:5]:
                print(f"    id={tid} repeats={cnt} from {t0.time()} to {t1.time()} box={box}")
            if len(r['stagnant_api'])>5:
                print("    ...")
        if r['ghost_draws']:
            print(f"  Suspected ghost draws (HANGOVER>3 with identical boxes): {len(r['ghost_draws'])}")
            for (cam, tid, t, detSeq, hang, box) in r['ghost_draws'][:10]:
                print(f"    t={t.time()} id={tid} hang={hang} detSeq={detSeq} box={box}")
            if len(r['ghost_draws'])>10:
                print("    ...")

if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("Usage: analyze_logs.py <log_file>")
        sys.exit(1)
    main(sys.argv[1])

