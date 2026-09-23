# 0014 — Localization with a managed gettext reader

- Status: Accepted
- Date: 2026-09-23

## Context and Problem Statement

`GettextCatalog` (`MonoDevelop.Core/Gettext.cs`) wraps `Mono.Unix.Catalog`, which P/Invokes native
libintl through Mono.Posix. Generated UI code calls `Mono.Unix.Catalog.GetString` directly (894
calls). Translations ship as `.po` → `.mo` files in `main/po`.

## Considered Options

1. NGettext managed `.mo` reader behind `GettextCatalog` (chosen)
2. Mono.Unix 7.1 `Catalog` over native libintl (native dependency, glibc-specific)
3. Convert translations to .resx (loses the gettext workflow)

## Decision Outcome

Implement `GettextCatalog` on NGettext (managed `.mo` reader, MIT), keeping the public API and the
existing `.mo` layout (`MONODEVELOP_LOCALE_PATH`). Direct `Mono.Unix.Catalog.GetString` calls in
ported code are rewritten to `GettextCatalog.GetString`.

### Consequences

- Good: no native libintl dependency; works in Flatpak and tests.
- Bad: mechanical rewrite of generated code as it is ported.
