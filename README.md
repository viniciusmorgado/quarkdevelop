**NOTICE**
-------------

**This project has not been built nor maintained since January 2020 and has been archived**

If you are interested in working on the project, even when archived you can still create a fork of it.

<br/><br/><br/>

---

<br/><br/><br/>


## Linux / .NET 10 migration (this fork)

This fork is migrating MonoDevelop to **.NET 10 LTS** and **GTK3**, Linux-first
(macOS and Windows are not supported). Plan, constitution and progress:
[`specs/001-linux-dotnet10-migration/`](specs/001-linux-dotnet10-migration/),
decisions in [`docs/adr/`](docs/adr/), removed features in
[`docs/BREAKING-CHANGES.md`](docs/BREAKING-CHANGES.md).

Build requirements on the host: **podman** and **git** only — everything runs in the dev container:

```bash
./scripts/pm ./scripts/setup.sh
./scripts/pm ./scripts/build.sh
./scripts/pm ./scripts/test.sh
```

Full guide: [`docs/linux/setup.md`](docs/linux/setup.md).

---

**MonoDevelop** is a full-featured integrated development environment (IDE) for mono using Gtk#.

The MonoDevelop core is also the foundation for Visual Studio for Mac.
Feel free to file bugs against Visual Studio for Mac here as well.

See http://www.monodevelop.com for more info.

[![Gitter](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/mono/monodevelop?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

Directory organization
----------------------

 * `main`: the MonoDevelop assemblies and add-ins. `main/MonoDevelop.Linux.sln` is the solution;
   `main/vendor` holds the forked dependencies (Xwt, vs-editor-api, …).
 * `scripts`: build, test, run and CI scripts, run inside the dev container with `./scripts/pm`.
 * `docs`, `specs`: documentation, architecture decisions and the migration plan.

Building, running and debugging
-------------------------------

```bash
./scripts/pm ./scripts/build.sh                 # main/build/bin and main/build/AddIns
./scripts/pm ./scripts/test.sh                  # unit tests and coverage
./scripts/pm ./scripts/run.sh                   # the IDE (see setup.md to show it on your desktop)
./scripts/pm ./scripts/debug.sh ide             # the IDE under netcoredbg
```

`./scripts/pm dotnet main/build/bin/mdtool.dll build <solution or project>` builds from the command line.
Problems and their fixes: [`docs/linux/troubleshooting.md`](docs/linux/troubleshooting.md).

References
----------

**[MonoDevelop website](http://www.monodevelop.com)**

**[Gnome Human Interface Guidelines (HIG)](https://developer.gnome.org/hig/stable/)**

**[freedesktop.org standards](http://freedesktop.org/Standards/)**

Discussion, Bugs, Patches
-------------------------

monodevelop-list@lists.ximian.com *(questions and discussion)*

monodevelop-patches-list@lists.ximian.com *(track commits to MonoDevelop)*

monodevelop-bugs@lists.ximian.com *(track MonoDevelop bugzilla component)*

https://github.com/mono/monodevelop/issues/new *(submit bugs and patches here)*

