# TownSuite.MsiCreator — Jekyll site

The source for the TownSuite.MsiCreator documentation/landing site, built with
[Jekyll](https://jekyllrb.com/) and deployed to GitHub Pages by the
[`jekyll.yml`](../.github/workflows/jekyll.yml) workflow on every push to `main`
that touches this folder.

## Local development

```bash
cd jekyll_site
bundle install
bundle exec jekyll serve --livereload
# open http://localhost:4000
```

## Editing content

Most brand/content settings live in `_config.yml` under the `townsuite:` key
(repo URL, accent color, version). The CLI flag reference table is data-driven —
edit `_data/flags.yml`. Page markup is in `index.html`; shared `<head>`/scripts
are in `_layouts/default.html`.

| Path | Purpose |
| --- | --- |
| `_config.yml` | Site + brand settings |
| `_data/flags.yml` | CLI flag reference table rows |
| `_layouts/default.html` | HTML shell (fonts, CSS/JS includes, SEO) |
| `index.html` | The single-page site |
| `assets/css/style.css` | Reset, keyframes, hover + responsive rules |
| `assets/js/main.js` | Copy-to-clipboard and tab interactions |
| `assets/townsuite-logo.svg` | Brand mark / favicon |
