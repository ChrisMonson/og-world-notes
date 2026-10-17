# og-world-notes

Writeups of the og-world runs, published with GitHub Pages: what each caretaker
was given, what it made, and what happened.

The code is in [og-world](https://github.com/ChrisMonson/og-world) and
[og-world-supervisor](https://github.com/ChrisMonson/og-world-supervisor).

## The site

It is plain static files: `index.html` and `assets/` are the page, `content/`
holds the writing and the hand-sorted commit labels (`content/commits.json`),
`log/` the caretakers' logs, and `play/` the playable worlds. To look at it
locally, serve the repository root:

    python3 -m http.server

`./build-site.sh` assembles exactly what is published into `_site/`;
`.github/workflows/pages.yml` runs it after building the playable engines.
`log/build.py` rebuilds the logs from the private archive, and `log/events.py`
the timelines from the logs.
