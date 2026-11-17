"""Answer backends (with a local HTTP server standing in for llama-server), the schema validator, the GBNF grammar and the tool runner wiring."""
import json
import sys
import threading
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path

import pytest

from conftest import ROOT
from training.common import backends as B, schema as S
from training.common.jsonio import load_json
from training.investigator import grammar as GR, protocol as P


# ---------------------------------------------------------------- backends

def test_fake_backend_passes_the_constraint_only_to_responders_that_take_it():
    seen = []
    b = B.FakeBackend(lambda messages, constraint: seen.append(constraint) or "ok")
    b.generate([{"role": "user", "content": "x"}], constraint={"kind": "report"})
    assert seen == [{"kind": "report"}]
    assert B.FakeBackend(lambda messages: "plain").generate([{"role": "user", "content": "x"}]).text == "plain"


class FakeLlama(BaseHTTPRequestHandler):
    requests: list = []

    def log_message(self, *a):
        pass

    def do_GET(self):
        self.send_response(200)
        self.end_headers()
        self.wfile.write(b'{"status": "ok"}')

    def do_POST(self):
        body = json.loads(self.rfile.read(int(self.headers["content-length"])))
        FakeLlama.requests.append((self.path, body))
        self.send_response(200)
        if self.path == "/apply-template":
            self.end_headers()
            self.wfile.write(json.dumps({"prompt": "".join(f"<{m['role']}>{m['content']}" for m in body["messages"])}).encode())
            return
        self.send_header("content-type", "text/event-stream")
        self.end_headers()
        for piece in ("He", "llo"):
            self.wfile.write(f'data: {json.dumps({"choices": [{"delta": {"content": piece}}]})}\n\n'.encode())
        self.wfile.write(b"data: [DONE]\n\n")


@pytest.fixture()
def llama_server():
    FakeLlama.requests = []
    srv = HTTPServer(("127.0.0.1", 0), FakeLlama)
    t = threading.Thread(target=srv.serve_forever, daemon=True)
    t.start()
    yield srv.server_address[1]
    srv.shutdown()


class FakeProc:
    def __init__(self):
        self.terminated = False

    def poll(self):
        return None

    def terminate(self):
        self.terminated = True

    def wait(self, timeout=None):
        pass


def test_llama_server_backend_starts_waits_for_health_and_streams(llama_server):
    started = []
    proc = FakeProc()
    b = B.LlamaServerBackend("/models/a.gguf", chat_template_kwargs={"enable_thinking": False}, port=llama_server, process_factory=lambda cmd: started.append(cmd) or proc)
    res = b.generate([{"role": "user", "content": "hi"}], max_new_tokens=50, constraint={"grammar": 'root ::= "x"'})
    assert res.text == "Hello" and res.ttft_ms is not None and res.total_ms >= res.ttft_ms
    cmd = started[0]
    assert cmd[0] == "llama-server" and "--n-gpu-layers" in cmd and cmd[cmd.index("--n-gpu-layers") + 1] == "999" and cmd[cmd.index("--ctx-size") + 1] == "4096"
    assert "127.0.0.1" in cmd and "--jinja" in cmd
    path, body = FakeLlama.requests[-1]
    assert path == "/v1/chat/completions" and body["temperature"] == 0 and body["max_tokens"] == 50 and body["chat_template_kwargs"] == {"enable_thinking": False}
    assert body["grammar"] == 'root ::= "x"' and body["stream"] is True
    assert b.apply_template([{"role": "system", "content": "s"}, {"role": "user", "content": "u"}]) == "<system>s<user>u"
    b.close()
    assert proc.terminated and len(started) == 1


def test_llama_server_cpu_forces_zero_layers_and_threads():
    b = B.LlamaServerBackend("/m.gguf", n_gpu_layers=0, threads=8, port=1234)
    cmd = b.command()
    assert cmd[cmd.index("--n-gpu-layers") + 1] == "0" and cmd[cmd.index("--threads") + 1] == "8" and b.info()["device"] == "cpu"


def test_heavy_backends_import_nothing_until_used():
    for name in ("torch", "transformers", "onnxruntime", "peft"):
        assert name not in sys.modules
    B.TransformersBackend("model-dir")
    B.OnnxBackend("export-dir")
    assert all(n not in sys.modules for n in ("torch", "transformers", "onnxruntime"))


def test_make_backend_reads_the_manifest_and_rejects_unknown_exports(tmp_path):
    manifest = {"exports": [{"format": "gguf", "files": [{"path": "assistant-1.0.0-Q4_K_M.gguf"}]}]}
    b = B.make_backend("gguf", tmp_path, kwargs={}, manifest=manifest, model="assistant", version="1.0.0", device="cpu")
    assert str(b.gguf).endswith("gguf/assistant-1.0.0-Q4_K_M.gguf") and b.n_gpu_layers == 0
    assert isinstance(B.make_backend("merged", tmp_path, kwargs={}, manifest=None, model="assistant", version="1"), B.TransformersBackend)
    assert isinstance(B.make_backend("onnx", tmp_path, kwargs={}, manifest=None, model="assistant", version="1"), B.OnnxBackend)
    with pytest.raises(B.BackendError):
        B.make_backend("gguf", tmp_path, kwargs={}, manifest={"exports": []}, model="assistant", version="1")
    with pytest.raises(B.BackendError):
        B.make_backend("tflite", tmp_path, kwargs={}, manifest=None, model="assistant", version="1")


# ---------------------------------------------------------------- the schema subset

SCHEMA = {"type": "object", "additionalProperties": False, "required": ["a"], "properties": {
    "a": {"type": "string", "pattern": "^x+$", "maxLength": 3}, "b": {"type": ["integer", "null"], "minimum": 1, "maximum": 5},
    "c": {"type": "array", "maxItems": 2, "items": {"$ref": "#/$defs/e"}}, "d": {"anyOf": [{"type": "null"}, {"type": "boolean"}]}},
    "$defs": {"e": {"type": "string", "enum": ["p", "q"]}}}


def test_schema_validator_accepts_and_rejects():
    assert S.validate(SCHEMA, {"a": "xx", "b": None, "c": ["p"], "d": True}) == []
    for bad, text in (({}, "missing"), ({"a": "xxxx"}, "longer"), ({"a": "y"}, "match"), ({"a": "x", "z": 1}, "unexpected"), ({"a": "x", "b": 9}, "above"),
                      ({"a": "x", "b": True}, "expected"), ({"a": "x", "c": ["p", "q", "p"]}, "more than"), ({"a": "x", "c": ["r"]}, "not one of"),
                      ({"a": "x", "d": "no"}, "none of the alternatives")):
        errs = S.validate(SCHEMA, bad)
        assert errs and any(text in e for e in errs), (bad, errs)


def test_tool_arguments_validate_against_the_catalog_with_the_entity_enum():
    cat = load_json(ROOT / "docs" / "support" / "tools.json")
    ok = P.tool_call_errors(cat, ["task"], "sql_entity_read", {"entity": "task", "id": "abc"})
    assert ok == []
    assert P.tool_call_errors(cat, ["task"], "sql_entity_read", {"entity": "Task", "id": "abc"})
    assert P.tool_call_errors(cat, ["task"], "logs_by_correlation", {"trace_id": "ABC"})
    assert P.tool_call_errors(cat, [], "delete_everything", {})
    assert P.tool_call_errors(cat, [], "signature_lookup", {"log_key": "a.b.c", "template_id": "t-000000000000"})
    assert P.tool_call_errors(cat, [], "signature_lookup", {})
    assert P.tool_call_errors(cat, [], "health_status", {}) == []


# ---------------------------------------------------------------- the grammar

def test_turn_grammar_is_well_formed_for_every_tool_and_the_report():
    cat = load_json(ROOT / "docs" / "support" / "tools.json")
    schema = P.investigation_schema()
    for only_report in (False, True):
        g = GR.turn_grammar(cat, schema, ["task", "user"], only_report)
        assert g.startswith("root ::=") and GR.undefined_rules(g) == []
        assert "<report>" in g and ("<call>" in g) == (not only_report)
    g = GR.turn_grammar(cat, schema, ["task", "user"])
    for t in cat["tools"]:
        assert t["name"] in g
    assert '"\\"task\\""' in g and "_" not in "".join(l.split(" ::= ")[0] for l in g.splitlines())          # rule names: letters, digits, dashes
