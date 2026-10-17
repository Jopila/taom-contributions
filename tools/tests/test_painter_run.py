#!/usr/bin/env python3
"""Unit tests for tools/painter/painter_run.py, without Painter.

Run:  python -m unittest tools.tests.test_painter_run

The wrapper is evaluated locally the way Painter's /run.json evaluates it, so the ARGS-in and
RESULT-out contract is proven without the bridge; a local socket that never answers stands in for
a Painter busy past the timeout.
"""
import contextlib
import importlib.util
import io
import json
import os
import socket
import tempfile
import threading
from http.server import BaseHTTPRequestHandler, HTTPServer
import unittest

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(TOOLS, "painter", "painter_run.py")
_spec = importlib.util.spec_from_file_location("painter_run", SCRIPT)
pr = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(pr)


class WrapTests(unittest.TestCase):
    def test_args_in_result_out(self):
        script = "RESULT = {'got': ARGS['x'] * 2, 'name': ARGS['name']}"
        out = eval(pr.wrap(script, {"x": 21, "name": "carn_dum"}))
        self.assertEqual(json.loads(out), {"got": 42, "name": "carn_dum"})

    def test_no_result_is_null(self):
        self.assertIsNone(json.loads(eval(pr.wrap("y = 1", {}))))

    def test_script_errors_propagate(self):
        with self.assertRaises(ZeroDivisionError):
            eval(pr.wrap("RESULT = 1 / 0", {}))


class ParseArgTests(unittest.TestCase):
    def test_json_values_parse(self):
        self.assertEqual(pr.parse_arg("n=1024"), ("n", 1024))
        self.assertEqual(pr.parse_arg("flag=true"), ("flag", True))
        self.assertEqual(pr.parse_arg("sets=[\"a\", \"b\"]"), ("sets", ["a", "b"]))

    def test_other_values_stay_strings(self):
        self.assertEqual(pr.parse_arg(r"low=E:\x\carn_dum.fbx"), ("low", r"E:\x\carn_dum.fbx"))
        self.assertEqual(pr.parse_arg("mode=False"), ("mode", "False"))  # JSON is lowercase: use false


class MainTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.script = os.path.join(self.tmp.name, "s.py")
        with open(self.script, "w", encoding="utf-8") as f:
            f.write("RESULT = 1\n")

    def tearDown(self):
        self.tmp.cleanup()

    def silent_server(self):
        """Accepts one connection and never answers: a Painter still busy with a long script."""
        srv = socket.socket()
        srv.bind(("localhost", 0))
        srv.listen(1)
        held = []

        def accept():
            try:
                conn, _ = srv.accept()
                held.append(conn)
            except OSError:
                pass

        threading.Thread(target=accept, daemon=True).start()
        self.addCleanup(lambda: [c.close() for c in held] and None)
        self.addCleanup(srv.close)
        return srv.getsockname()[1]

    def test_timeout_is_not_reported_as_unreachable(self):
        port = self.silent_server()
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = pr.main([self.script, "--port", str(port), "--timeout", "0.5"])
        self.assertEqual(code, 1)
        self.assertIn("no answer within", out.getvalue())
        self.assertNotIn("cannot reach", out.getvalue())

    def test_refused_connection_says_cannot_reach(self):
        s = socket.socket()
        s.bind(("localhost", 0))
        port = s.getsockname()[1]
        s.close()
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = pr.main([self.script, "--port", str(port), "--timeout", "2"])
        self.assertEqual(code, 1)
        self.assertIn("cannot reach Painter", out.getvalue())

    def test_error_dict_without_known_keys_prints_itself(self):
        class Handler(BaseHTTPRequestHandler):
            def do_POST(self):
                self.rfile.read(int(self.headers["Content-Length"]))
                body = b'{"x": 1}'
                self.send_response(200)
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)

            def log_message(self, *args):
                pass

        srv = HTTPServer(("localhost", 0), Handler)
        threading.Thread(target=srv.serve_forever, daemon=True).start()
        self.addCleanup(srv.server_close)
        self.addCleanup(srv.shutdown)
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = pr.main([self.script, "--port", str(srv.server_address[1]), "--timeout", "5"])
        self.assertEqual(code, 1)
        self.assertIn("{'x': 1}", out.getvalue())


if __name__ == "__main__":
    unittest.main()
