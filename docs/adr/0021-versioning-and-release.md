# 0021 — Versioning and releases

- Status: Accepted
- Date: 2026-09-24

## Context and Problem Statement

`version.config` holds the product version the code depends on: `Version=8.6` and `CompatVersion=8.0`.
They name the user profile directory (`~/.config/MonoDevelop/8.0`), the add-in compatibility range in
the Mono.Addins manifests and the add-in repository URLs. The constitution asks for versioned CI
artifacts (SemVer and SHA) and releases triggered by a tag. The first release of this fork is
`v0.1.0-linux` (M9, T131). How do the product version and the release version relate, and what does a tag
produce?

## Considered Options

1. Keep `version.config` for the product; take the release version from the git tag.
2. Rewrite `version.config` to the release SemVer (`0.1.0`).
3. MinVer or GitVersion computing the version from tags on every build.

## Decision Outcome

Option 1.
- **Product version:** `version.config` (`8.6`, compatibility `8.0`) stays unchanged. Changing it would
  move the profile directory and break add-in compatibility (option 2).
- **Release version:** a SemVer 2.0 tag `v<major>.<minor>.<patch>[-<pre-release>]` on the migration
  branch, for example `v0.1.0-linux`. A tag with a pre-release part makes a GitHub pre-release.
- **CI artifacts:** `monodevelop-ci-<version.config Version>+<commit SHA>` (T116).
- **Release artifacts:** `monodevelop-<tag version>+<short SHA>`.

`.github/workflows/release.yml` runs on `v*` tags. The job summary and the release body show the tag version and the SHA.
- The `build` job (`contents: read`) checks that the tag is SemVer and runs the same gate as CI
  (`scripts/ci.sh`).
- Once M7 adds `scripts/package-flatpak.sh`, the same job builds `out/monodevelop.flatpak` with its
  `.sha256` and the CycloneDX SBOM.
- The `publish` job is the only one with `contents: write`. It creates the GitHub release from the
  uploaded artifacts and `docs/BREAKING-CHANGES.md`.

MinVer (option 3) would stamp every assembly with a version that no code reads, and it adds a build-time
dependency on tag history. Shallow clones would build with a different version.

### Consequences

- Good: the tag is the only input a release needs, and the profile and add-in compatibility do not move.
- Good: least privilege, since only the publish job can write.
- Bad: the About dialog shows `8.6`, not the release version. Stamping the release version into the
  informational version is left for M9 if the release notes need it.
- Pushing tags and publishing releases need the maintainer's explicit authorization (constitution,
  governance).
