"""Hashing, seeds, JSONL, templates, estimate arithmetic and approval, teacher cache and resume, hardware."""
import hashlib
import json
import threading
from pathlib import Path

import pytest

from training.common import config as C, estimate as E, hardware as H, hashing as Hh, jsonio, teacher as T
from training.common.runs import RunRecorder
from training.common.templates import Template


# ---- hashing and seeds

def test_seed_is_the_first_eight_hex_digits_of_the_hash():
    want = int(hashlib.sha256(b"20261106|d07/it/questions/US-INV-03/symptom").hexdigest()[0:8], 16)
    assert Hh.derive_seed(20261106, "d07/it/questions/US-INV-03/symptom") == want
    assert Hh.derive_seed(1, "a") != Hh.derive_seed(2, "a") != Hh.derive_seed(2, "b")


def test_rng_and_shuffle_are_deterministic():
    assert Hh.shuffled(list(range(20)), 5, "k") == Hh.shuffled(list(range(20)), 5, "k")
    assert Hh.shuffled(list(range(20)), 5, "k") != Hh.shuffled(list(range(20)), 5, "other")
    assert Hh.rng(5, "k").random() == Hh.rng(5, "k").random()


def test_git_blob_id_known_value():
    assert Hh.git_blob_id(b"hello\n") == "ce013625030ba8dba906f756967f9e9ca394464a"


def test_canonical_hash_ignores_key_order():
    assert Hh.canonical_sha256({"a": 1, "b": [1, 2]}) == Hh.canonical_sha256({"b": [1, 2], "a": 1})


def test_jsonl_round_trip_and_append(tmp_path):
    p = tmp_path / "x" / "a.jsonl"
    jsonio.write_jsonl(p, [{"b": 1, "a": "è"}, {"c": 2}])
    jsonio.append_jsonl(p, {"d": 3})
    assert jsonio.read_jsonl(p) == [{"b": 1, "a": "è"}, {"c": 2}, {"d": 3}]
    assert p.read_text(encoding="utf-8").splitlines()[0] == '{"b":1,"a":"è"}'      # key order kept, no ASCII escapes
    assert not list(p.parent.glob("*.tmp"))


def test_run_ids(tmp_path):
    from datetime import date
    d = date(2026, 11, 20)
    assert C.next_run_id("assistant", runs=tmp_path / "runs", today=d) == "2026-11-20-a"
    (tmp_path / "runs" / "2026-11-20-a").mkdir(parents=True)
    assert C.next_run_id("assistant", runs=tmp_path / "runs", today=d) == "2026-11-20-b"
    assert C.RUN_ID_RE.match("2026-11-20-b")


def test_run_recorder_writes_run_json(tmp_path):
    with RunRecorder("assistant", "generate", {"dataset": "abc"}, model_out=tmp_path, argv=["--x"]) as rec:
        rec.extra["note"] = "n"
    data = json.loads(rec.path.read_text())
    assert data["commands"][0]["command"] == "generate" and data["commands"][0]["config_sha256"] == {"dataset": "abc"}
    assert C.RUN_ID_RE.match(data["run_id"])
    with RunRecorder("assistant", "validate", run_id=rec.run_id, model_out=tmp_path):
        pass
    assert [c["command"] for c in json.loads(rec.path.read_text())["commands"]] == ["generate", "validate"]


def test_the_code_commit_is_unknown_without_git(tmp_path, monkeypatch):
    # A copy that is not a git repository, or a machine without git: the commit is recorded as unknown (null)
    import subprocess
    from training.common import runs

    monkeypatch.setenv("GIT_CEILING_DIRECTORIES", str(tmp_path.parent))
    assert runs.code_commit(tmp_path) is None

    def no_git(*a, **k):
        raise FileNotFoundError("git")
    monkeypatch.setattr(subprocess, "run", no_git)
    assert runs.code_commit(tmp_path) is None
    with RunRecorder("assistant", "generate", model_out=tmp_path) as rec:
        pass
    assert json.loads(rec.path.read_text())["commands"][0]["code_commit"] is None


# ---- templates

def test_template_renders_system_and_user(tmp_path):
    (tmp_path / "t.v1.txt").write_text("sys {{a}}\n=== USER ===\nuser {{b}} {{a}}\n", encoding="utf-8")
    t = Template.load(tmp_path, "t.v1")
    assert t.render({"a": "1", "b": ["x"]}) == ("sys 1", 'user ["x"] 1')
    assert t.sha256 == hashlib.sha256((tmp_path / "t.v1.txt").read_bytes()).hexdigest()
    with pytest.raises(KeyError):
        t.render({"a": "1"})


def test_template_substitution_is_single_pass(tmp_path):
    (tmp_path / "t.v1.txt").write_text("{{a}} {{b}}", encoding="utf-8")
    assert Template.load(tmp_path, "t.v1").render({"a": "{{b}}", "b": "x"}) == ("", "{{b}} x")


# ---- estimate

RATES = {"model": "big", "verifier_model": "small", "tokens_per_second": 20.0, "prompt_tokens_per_second": 200.0}


def test_estimate_arithmetic():
    rows = [("answers", "big", 3500, 400), ("answers", "big", 7000, 400), ("verify", "small", 3500, 200)]
    a, v = E.estimate_stages(rows, RATES)
    assert (a.stage, a.requests) == ("answers", 2)
    assert a.input_tokens == 3000                     # (3500 + 7000) / 3.5
    assert a.output_tokens == 480                     # 2 x 0.6 x 400
    assert a.seconds == pytest.approx(480 / 20 + 3000 / 200)      # generation plus prompt processing
    assert v.input_tokens == 1000 and v.output_tokens == 120
    assert v.seconds == pytest.approx(120 / 20 + 1000 / 200)


def test_estimate_without_a_known_speed_has_no_duration():
    (a,) = E.estimate_stages([("a", "m", 350, 10)], {"tokens_per_second": 0})
    assert a.output_tokens == 6 and a.seconds == 0.0
    assert E.rates_of({"tokens_per_second": "bad"}) == {"tokens_per_second": 0.0, "prompt_tokens_per_second": 0.0}
    assert E.rates_of({"tokens_per_second": 5, "prompt_tokens_per_second": -1}) == {"tokens_per_second": 5.0, "prompt_tokens_per_second": 0.0}


def test_slower_host_means_longer_estimate():
    rows = [("a", "m", 3500, 100)] * 4
    fast, slow = (E.estimate_stages(rows, {"tokens_per_second": t})[0].seconds for t in (40, 10))
    assert slow == pytest.approx(4 * fast)


def test_duration_format():
    assert E.format_duration(45) == "45s" and E.format_duration(125) == "2m 05s" and E.format_duration(7384) == "2h 03m"


def test_estimate_id_depends_on_config_counts_and_rates():
    stages = E.estimate_stages([("a", "big", 350, 10)], RATES)
    base = E.estimate_id({"x": 1}, stages, {"p": 1})
    assert len(base) == 12 and base == E.estimate_id({"x": 1}, stages, {"p": 1})
    assert base != E.estimate_id({"x": 2}, stages, {"p": 1})
    assert base != E.estimate_id({"x": 1}, stages, {"p": 2})
    more = E.estimate_stages([("a", "big", 350, 10)] * 2, RATES)
    assert base != E.estimate_id({"x": 1}, more, {"p": 1})
    same_counts_other_size = E.estimate_stages([("a", "big", 3500, 10)], RATES)
    assert base == E.estimate_id({"x": 1}, same_counts_other_size, {"p": 1})     # id covers counts, not sizes


def test_approval():
    E.require_approval("abc", "abc")
    with pytest.raises(E.ApprovalError, match="--approved-estimate abc"):
        E.require_approval("abc", None)
    with pytest.raises(E.ApprovalError, match="does not match"):
        E.require_approval("abc", "zzz")


def test_table_has_total_row():
    t = E.format_table(E.estimate_stages([("a", "big", 350, 10)], RATES))
    assert "total" in t.splitlines()[-1] and t.splitlines()[0].startswith("stage")


# ---- teacher jobs, cache, resume

class Counting:
    def __init__(self, fail_first=0, cost=(10, 20)):
        self.calls = []
        self.fail_first = fail_first
        self.lock = threading.Lock()
        self.cost = cost

    def complete(self, *, model, system, user, params, seed):
        with self.lock:
            self.calls.append(user)
            if self.fail_first:
                self.fail_first -= 1
                raise T.TransientTeacherError("429")
        return T.Completion(f"answer to {user}", *self.cost)


def make_jobs(tmp_path, n=5):
    (tmp_path / "t.v1.txt").write_text("prompt {{i}}", encoding="utf-8")
    t = Template.load(tmp_path, "t.v1")
    return t, [T.make_job(stage="s", key=f"d01/en/s/{i}", domain="d01", lang="en", template=t, variables={"i": i}, model="m", params={"temperature": 0, "max_tokens": 10}, master_seed=1) for i in range(n)]


def runner(tmp_path, client, max_seconds=None, **kw):
    t, jobs = make_jobs(tmp_path)
    cache = T.JobCache(tmp_path / "cache")
    r = T.JobRunner(cache, client, lambda j: t.render(j["vars"]), max_seconds, sleep=lambda s: None, **kw)
    return r, cache, jobs


def test_job_record_format():
    t = Template("q.v1", "x", "ab" * 32)
    j = T.make_job(stage="questions", key="d07/it/questions/US-INV-03/symptom", domain="d07", lang="it", template=t, variables={}, model="m", params={"temperature": 0.8, "max_tokens": 1500}, master_seed=20261106)
    assert list(j) == ["schema_version", "stage", "key", "domain", "lang", "template", "template_sha256", "vars", "model", "params", "seed"]
    assert j["seed"] == Hh.derive_seed(20261106, j["key"])


def test_cache_path_is_two_level_hash_of_the_job(tmp_path):
    r, cache, jobs = runner(tmp_path, Counting())
    h = T.job_hash(jobs[0])
    assert cache.path(h) == tmp_path / "cache" / h[:2] / f"{h}.json"
    assert h == Hh.canonical_sha256(jobs[0])


def test_run_then_resume_sends_nothing_twice(tmp_path):
    client = Counting()
    r, cache, jobs = runner(tmp_path, client)
    res = r.run(jobs)
    assert (res.sent, res.cached, res.complete) == (5, 0, True) and len(client.calls) == 5
    res2 = r.run(jobs)
    assert (res2.sent, res2.cached) == (0, 5) and len(client.calls) == 5
    entry = cache.get(jobs[0])
    assert entry["response"] == "answer to prompt 0" and entry["usage"] == {"input_tokens": 10, "output_tokens": 20}


def test_changed_job_is_a_new_cache_entry(tmp_path):
    client = Counting()
    r, cache, jobs = runner(tmp_path, client)
    r.run(jobs[:2])
    changed = dict(jobs[0], params={"temperature": 0.5, "max_tokens": 10})
    assert cache.get(changed) is None


class Clock:
    """A clock that advances 1.0 each time it is read: every job takes a known time."""

    def __init__(self):
        self.t = 0.0

    def __call__(self):
        self.t += 1.0
        return self.t


def test_time_limit_stops_cleanly_and_a_later_run_resumes(tmp_path):
    client = Counting()
    r, cache, jobs = runner(tmp_path, client, max_seconds=5.0, max_concurrency=1, clock=Clock())
    res = r.run(jobs)
    assert res.stopped_for_time and 0 < res.sent < 5 and res.pending == 5 - res.sent and not res.complete
    r2 = T.JobRunner(cache, client, r.render, None, 1, sleep=lambda s: None)
    res2 = r2.run(jobs)
    assert res2.complete and res2.cached == res.sent and res2.sent + res2.cached == 5


def test_no_time_limit_means_the_run_finishes(tmp_path):
    r, cache, jobs = runner(tmp_path, Counting(), max_seconds=None, max_concurrency=1, clock=Clock())
    res = r.run(jobs)
    assert res.complete and not res.stopped_for_time and res.elapsed_seconds > 0
    assert cache.get(jobs[0])["seconds"] > 0 and "cost_usd" not in cache.get(jobs[0])


def test_transient_errors_are_retried_and_hard_errors_recorded(tmp_path):
    client = Counting(fail_first=2)
    r, cache, jobs = runner(tmp_path, client, max_concurrency=1)
    assert r.run(jobs[:1]).complete

    class Hard:
        def complete(self, **kw):
            raise T.TeacherError("HTTP 400")
    (tmp_path / "x").mkdir()
    r2, _c, jobs2 = runner(tmp_path / "x", Hard())
    res = r2.run(jobs2[:2])
    assert len(res.failed) == 2 and "HTTP 400" in res.failed[0][1] and not res.complete


class Stand:
    """A local stand-in for the OpenAI-compatible endpoint: records every request it receives."""

    def __init__(self):
        import http.server as hs
        outer = self
        self.requests = []
        self.redirect_to = None

        class H(hs.BaseHTTPRequestHandler):
            def log_message(self, *a):
                pass

            def do_POST(self):
                n = int(self.headers.get("content-length", 0))
                body = json.loads(self.rfile.read(n) or b"{}")
                outer.requests.append({"path": self.path, "headers": {k.lower(): v for k, v in self.headers.items()}, "body": body})
                if outer.redirect_to:
                    self.send_response(307)
                    self.send_header("location", outer.redirect_to + self.path)
                    self.end_headers()
                    return
                mt = body.get("max_tokens", 0)
                data = json.dumps({"choices": [{"message": {"content": "ok " * min(mt, 5)}}],
                                   "usage": {"prompt_tokens": len(json.dumps(body["messages"])) // 4, "completion_tokens": mt}}).encode()
                self.send_response(200)
                self.send_header("content-type", "application/json")
                self.send_header("content-length", str(len(data)))
                self.end_headers()
                self.wfile.write(data)

        self.server = hs.ThreadingHTTPServer(("127.0.0.1", 0), H)
        self.url = f"http://127.0.0.1:{self.server.server_address[1]}/v1"
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()

    def close(self):
        self.server.shutdown()
        self.server.server_close()


@pytest.fixture
def stand():
    s = Stand()
    yield s
    s.close()


def test_default_url_is_the_local_teacher_host():
    assert T.DEFAULT_BASE_URL == "http://localhost:8093/v1"
    assert T.HttpTeacherClient().base_url == "http://localhost:8093/v1"
    assert T.HttpTeacherClient(None, None).base_url == "http://localhost:8093/v1"
    assert T.HttpTeacherClient("http://host:1/v1/").base_url == "http://host:1/v1"
    with pytest.raises(ValueError):
        T.HttpTeacherClient("localhost:8093")


def test_the_shipped_configs_use_the_local_endpoint_and_no_key_by_default():
    root = Path(__file__).resolve().parents[1] / "assistant" / "config"
    for name, section in (("dataset.json", "teacher"), ("eval.json", "judge")):
        cfg = json.loads((root / name).read_text(encoding="utf-8"))[section]
        assert cfg["base_url"] == T.DEFAULT_BASE_URL and cfg["api_key_env"] is None
        assert "provider" not in cfg and "max_cost_usd" not in cfg and "price_per_million_tokens" not in cfg
        assert "tokens_per_second" in cfg


def test_the_key_is_optional_and_sent_only_when_the_variable_is_set(stand):
    kw = dict(model="m", system="s", user="u", params={"max_tokens": 4}, seed=1)
    T.HttpTeacherClient(stand.url).complete(**kw)                                           # no variable named
    T.HttpTeacherClient(stand.url, "MY_KEY", environ={}).complete(**kw)                     # named but not set
    T.HttpTeacherClient(stand.url, "MY_KEY", environ={"MY_KEY": ""}).complete(**kw)         # set to empty
    assert all("authorization" not in r["headers"] for r in stand.requests) and len(stand.requests) == 3
    T.HttpTeacherClient(stand.url, "MY_KEY", environ={"MY_KEY": "sekret"}).complete(**kw)
    assert stand.requests[-1]["headers"]["authorization"] == "Bearer sekret"


def test_request_shape_and_usage(stand):
    c = T.HttpTeacherClient(stand.url).complete(model="m", system="sys", user="usr", params={"max_tokens": 7, "temperature": 0.3}, seed=2 ** 33 + 5)
    r = stand.requests[0]
    assert r["path"] == "/v1/chat/completions"
    assert r["body"]["messages"] == [{"role": "system", "content": "sys"}, {"role": "user", "content": "usr"}]
    assert r["body"]["max_tokens"] == 7 and r["body"]["temperature"] == 0.3 and r["body"]["seed"] == 5
    assert c.output_tokens == 7 and c.input_tokens > 0 and c.text.startswith("ok")


def test_a_down_host_is_a_transient_error_naming_the_url():
    import socket
    sock = socket.socket()
    sock.bind(("127.0.0.1", 0))
    port = sock.getsockname()[1]
    sock.close()
    with pytest.raises(T.TransientTeacherError, match=f"127.0.0.1:{port}"):
        T.HttpTeacherClient(f"http://127.0.0.1:{port}/v1", timeout=2).complete(model="m", system="", user="u", params={}, seed=1)


def test_no_request_goes_to_a_host_that_was_not_configured(stand, monkeypatch):
    # 1. every URL the client opens starts with the configured base URL, whatever the environment says
    import urllib.request
    monkeypatch.setenv("HTTP_PROXY", "http://127.0.0.1:9")
    monkeypatch.setenv("http_proxy", "http://127.0.0.1:9")
    seen = []
    real_open = urllib.request.OpenerDirector.open

    def spy(self, fullurl, *a, **kw):
        seen.append(fullurl.full_url if hasattr(fullurl, "full_url") else fullurl)
        return real_open(self, fullurl, *a, **kw)
    monkeypatch.setattr(urllib.request.OpenerDirector, "open", spy)
    T.HttpTeacherClient(stand.url).complete(model="m", system="", user="u", params={}, seed=1)
    assert seen and all(u.startswith(stand.url) for u in seen) and len(stand.requests) == 1
    # 2. a redirect to another host is refused, and the other host receives nothing
    other = Stand()
    try:
        stand.redirect_to = other.url.rsplit("/v1", 1)[0]
        with pytest.raises((T.TeacherError, T.TransientTeacherError)):
            T.HttpTeacherClient(stand.url).complete(model="m", system="", user="u", params={}, seed=1)
        assert other.requests == []
    finally:
        other.close()


def test_the_pipeline_code_and_configs_name_no_remote_endpoint():
    """The only URLs in the teacher, judge and dataset code are local ones; export.json names source repositories to build tools from."""
    import re
    root = Path(__file__).resolve().parents[1]
    files = [p for d in ("common", "assistant") for p in (root / d).rglob("*") if p.suffix in (".py", ".json") and p.name != "export.json"]
    urls = {u for p in files for u in re.findall(r"https?://[A-Za-z0-9.:_-]+", p.read_text(encoding="utf-8"))}
    assert all(re.match(r"https?://(localhost|127\.0\.0\.1)(:\d*)?$", u) for u in urls if not u.startswith("https://:")), urls


# ---- measurement of the local host

def test_measure_throughput_against_a_local_stand_in(stand):
    m = E.measure_throughput(T.HttpTeacherClient(stand.url), "m", concurrency=3, max_tokens=64)
    assert len(stand.requests) == 4                                   # one prompt probe and three generation probes
    assert stand.requests[0]["body"]["max_tokens"] == 1
    assert m["generated_tokens"] == 3 * 64 and m["concurrency"] == 3
    assert m["tokens_per_second"] > 0 and m["prompt_tokens_per_second"] > 0
    text = E.format_measurement(m)
    assert "tokens_per_second" in text and "prompt_tokens_per_second" in text


def test_measure_uses_the_clock_it_is_given():
    class Fixed:
        def complete(self, **kw):
            return T.Completion("x", 500, kw["params"]["max_tokens"])
    ticks = iter([0.0, 2.0, 2.0, 12.0])
    m = E.measure_throughput(Fixed(), "m", concurrency=1, max_tokens=100, clock=lambda: next(ticks))
    assert m["prompt_tokens_per_second"] == 250.0 and m["tokens_per_second"] == 10.0


def test_measure_command_reports_and_writes_nothing(stand, tmp_path, monkeypatch):
    from training.assistant import generate as G
    cfg, _d, _r = G.load_all()
    cfg["teacher"].update(base_url=stand.url, model="served-name")
    lines = []
    monkeypatch.chdir(tmp_path)
    assert G.cmd_measure(cfg, 2, out=lines.append) == 0
    assert "tokens_per_second" in lines[0] and len(stand.requests) == 3
    assert list(tmp_path.iterdir()) == []
    cfg["teacher"]["model"] = "{teacher model name as served by the local host}"
    assert G.cmd_measure(cfg, 2, out=lines.append) == 2


def test_measure_command_reports_a_host_that_is_not_running():
    from training.assistant import generate as G
    cfg, _d, _r = G.load_all()
    cfg["teacher"].update(base_url="http://127.0.0.1:1/v1", model="m")
    lines = []
    assert G.cmd_measure(cfg, 1, out=lines.append) == 1 and "failed" in lines[0]


# ---- hardware

def info(**kw):
    base = dict(device="cuda", name="GPU", ram_gb=64.0, vram_gb=24.0, disk_free_gb=500.0, cpu_cores=16, python="3.12.1", verified_by="torch", notes=[])
    base.update(kw)
    return H.HardwareInfo(**base)


def test_hardware_rules(monkeypatch):
    from types import SimpleNamespace
    monkeypatch.setattr(H, "sys", SimpleNamespace(version_info=(3, 12, 1)))
    assert H.evaluate(info())[0] is True
    ok, problems = H.evaluate(info(vram_gb=8.0))
    assert not ok and any("VRAM" in p for p in problems)
    assert not H.evaluate(info(device="cpu", vram_gb=None))[0]
    ok, problems = H.evaluate(info(device="mps", vram_gb=None, ram_gb=16.0), model_parameters=1_000_000_000)
    assert not ok and any("32 GB" in p for p in problems)
    assert H.evaluate(info(device="mps", vram_gb=None, ram_gb=16.0))[0] is True
    assert not H.evaluate(info(disk_free_gb=10.0))[0]
    monkeypatch.setattr(H, "sys", SimpleNamespace(version_info=(3, 9, 0)))
    assert any("3.11 or 3.12" in p for p in H.evaluate(info())[1])


def test_batch_plan_reaches_the_effective_batch():
    for i in (info(vram_gb=12.0), info(vram_gb=48.0), info(device="mps", vram_gb=None, ram_gb=16.0)):
        plan = H.micro_batch_plan(i)
        assert plan["micro_batch"] * plan["grad_accum"] == plan["effective_batch"] == 32


def test_hardware_main_runs_without_torch(capsys):
    code = H.main(["--json"])
    out = json.loads(capsys.readouterr().out)
    assert code in (0, 1) and out["hardware"]["device"] in ("cuda", "mps", "cpu")
    assert (code == 0) == (out["hardware"]["device"] != "cpu")
