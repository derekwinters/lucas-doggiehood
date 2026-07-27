#!/usr/bin/env python3
"""Tiny shared GitHub REST API helper for the gatekeeper's glue scripts.

Stdlib `urllib` only, no third-party deps — matches the rest of the pipeline
(`pipeline-reconcile/reconcile.py`, `pipeline-dashboard/render_dashboard.py`).
Not unit-tested: it is a thin wrapper over real network I/O, exercised only by
the glue scripts that import it (`run_comment_event.py`, `run_sweep.py`).
"""

import json
import urllib.request

API = "https://api.github.com"


def request(method, path, token, body=None):
    """Issue one GitHub REST API call and return the parsed JSON body (or
    None for an empty response, e.g. a reaction POST)."""
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(API + path, data=data, method=method)
    req.add_header("Authorization", "Bearer %s" % token)
    req.add_header("Accept", "application/vnd.github+json")
    req.add_header("User-Agent", "doggiehood-gatekeeper")
    if data is not None:
        req.add_header("Content-Type", "application/json")
    with urllib.request.urlopen(req) as resp:
        raw = resp.read()
        return json.loads(raw) if raw else None
