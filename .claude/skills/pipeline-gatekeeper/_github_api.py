#!/usr/bin/env python3
"""Tiny shared GitHub REST API helper for the gatekeeper's glue scripts.

Stdlib `urllib` only, no third-party deps — matches the rest of the pipeline
(`pipeline-reconcile/reconcile.py`, `pipeline-dashboard/render_dashboard.py`).
Not unit-tested: it is a thin wrapper over real network I/O, exercised only by
the glue scripts that import it (`run_comment_event.py`, `run_sweep.py`).
"""

import json
import os
import subprocess
import sys
import urllib.request

API = "https://api.github.com"


def rerender_dashboard(repo, token, focus_title=None, cap=None):
    """Re-render the pipeline dashboard (#193) INLINE — never a body PATCH.

    Shells out to `pipeline-dashboard/render_dashboard.py --write` as a
    subprocess in the same gatekeeper job, reusing the job's `GITHUB_TOKEN`.
    Because no second workflow *run* is created, the `GITHUB_TOKEN`
    no-recursion guard never applies (issue #442, Option B): the gatekeeper's
    own automated label moves refresh the dashboard immediately, whereas the
    `issues: [labeled, unlabeled]` trigger on `dashboard.yml` only covers
    human/UI/PAT-authored changes.

    Called once per gatekeeper run, after all label changes are applied — the
    renderer recomputes full board state regardless, so a single render
    reflects every issue touched this run.

    `focus_title` / `cap`, when given, persist a `/focus` / `/cap` marker via
    the `DASHBOARD_SET_FOCUS` / `DASHBOARD_SET_CAP` overrides rather than a
    read-modify-write of #193's body, which would re-HTML-encode it and break
    the Mermaid charts (#204). This restores the `/focus` path dropped in #238
    and reuses `/cap`'s seam (#240).
    """
    env = dict(os.environ)
    env["GITHUB_TOKEN"] = token
    env["DASHBOARD_REPO"] = repo
    if focus_title is not None:
        env["DASHBOARD_SET_FOCUS"] = focus_title
    if cap is not None:
        env["DASHBOARD_SET_CAP"] = str(cap)
    renderer = os.path.join(
        os.path.dirname(os.path.abspath(__file__)), os.pardir,
        "pipeline-dashboard", "render_dashboard.py")
    subprocess.run([sys.executable, renderer, "--write"], env=env, check=True)


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
