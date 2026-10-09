"""Gerçek bir API + SQL Server üzerinde eş zamanlılık yük testi (docker compose up ile ayağa kalkmış olmalı).

Kullanım:  python -X utf8 scripts/concurrency_load_test.py

Senaryo A: N kullanıcı aynı anda AYNI koltuğu tutmaya çalışır -> tam 1 adet 201, N-1 adet 409 beklenir.
Senaryo B: 200 kullanıcı 20 koltuktan rastgele birini seçer -> her koltuk için tam 1 kazanan.
Boş koltuk gerektirir: örnek veri için DEMO_ENABLED=true ile başlat. Test, tutma süresi (10 dk) dolana kadar koltukları meşgul eder.
"""
import json, time, threading, urllib.request, urllib.error, uuid, collections, random, statistics
from concurrent.futures import ThreadPoolExecutor

BASE = "http://localhost:8080"

def call(method, path, body=None, token=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            raw = r.read()
            return r.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        return e.code, None

def make_user(i):
    email = f"bench{i}_{uuid.uuid4().hex[:8]}@bench.local"
    s, r = call("POST", "/api/auth/register", {"email": email, "password": "Bench_Pass_123!"})
    return r["token"] if s == 201 else None

MAXN = 300
t0 = time.time()
with ThreadPoolExecutor(30) as ex:
    TOKENS = [t for t in ex.map(make_user, range(MAXN)) if t]
print(f"{len(TOKENS)} kullanıcı hazır ({time.time()-t0:.0f} sn)")

events = call("GET", "/api/events")[1]

def free_seats():
    out = []
    for ev in events:
        s, seats = call("GET", f"/api/events/{ev['id']}/seats", token=TOKENS[0])
        lst = seats if isinstance(seats, list) else seats.get("seats", [])
        out += [x["id"] for x in lst if x["status"] == "Available"]
    return out

def storm(targets_per_user):
    """Hepsi aynı anda başlar; (durum kodu, süre ms) listesi döner."""
    n = len(targets_per_user)
    barrier = threading.Barrier(n)
    def one(args):
        tok, seat = args
        barrier.wait()
        t = time.perf_counter()
        code = call("POST", f"/api/seats/{seat}/hold", None, tok)[0]
        return code, (time.perf_counter() - t) * 1000
    with ThreadPoolExecutor(n) as ex:
        return list(ex.map(one, targets_per_user))

pool = free_seats()
random.shuffle(pool)
print("boş koltuk:", len(pool))
rows = []
ROUNDS = 3
for n in (10, 50, 100, 200, 300):
    created = conflict = other = 0
    lat = []
    for _ in range(ROUNDS):
        seat = pool.pop()
        res = storm([(TOKENS[i], seat) for i in range(n)])
        c = collections.Counter(code for code, _ in res)
        created += c.get(201, 0); conflict += c.get(409, 0)
        other += sum(v for k, v in c.items() if k not in (201, 409))
        lat += [ms for _, ms in res]
    lat.sort()
    p95 = lat[int(len(lat) * 0.95) - 1]
    rows.append((n, ROUNDS, created, conflict, other, statistics.median(lat), p95))
    print(f"N={n} tur={ROUNDS} 201={created} 409={conflict} diğer={other} p50={statistics.median(lat):.0f}ms p95={p95:.0f}ms")

# Senaryo B: 200 kullanıcı, 20 koltuk, herkes rastgele birini seçer; her koltuk için tam 1 kazanan olmalı
seats20 = [pool.pop() for _ in range(20)]
picks = [(TOKENS[i], random.choice(seats20)) for i in range(200)]
distinct = len({s for _, s in picks})
res = storm(picks)
c = collections.Counter(code for code, _ in res)
print(f"B: 200 kullanıcı / 20 koltuk, seçilen farklı koltuk={distinct}, 201={c.get(201,0)}, 409={c.get(409,0)}, diğer={sum(v for k,v in c.items() if k not in (201,409))}")
print("JSON:", json.dumps({"rows": rows, "B": {"distinct": distinct, "created": c.get(201, 0), "conflict": c.get(409, 0)}}))
