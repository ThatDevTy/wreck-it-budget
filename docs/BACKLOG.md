# Backlog

Post-v1.0 ideas. Things land here instead of in the build.
An item moves to `PROGRESS.md` only once it's deliberately picked up.

_Initial entries drafted by Claude from the approved project plan, 2026-09-19._

---

## Features

- **Export** — data and/or reports. A real want, not a maybe. Format and scope are still to be
  workshopped.
- **Character customization** — rename the characters and swap in new sprites. The current
  names (Oscar the Wrecker, Mary the Fixer, Billy the Racer-Guide) are
  placeholders the user made up, used in the README for now. In v1.0, keep each character's
  name and sprite paths in one config file rather than scattered through templates. That
  costs nothing and makes this item cheap later (same reasoning as D-015).

## Maintenance

- **Apply a SQL Server cumulative update.** The install is 2025 RTM (17.0.1000.7) with no
  cumulative updates applied yet.

## Future decisions (not features)

- **License.** The repo is public with no LICENSE file, which means all rights reserved: people
  can read the code but can't reuse it. That's fine for a portfolio. Decide later whether to
  add one (e.g. MIT) and whether the sprite art should be covered by it.

- **Hosting beyond the LAN** — a separate conversation covering HTTPS, secrets, backups, and
  uptime. See D-013. If this ever happens, D-012 has to be revisited as well.

## Explicitly out of scope

Listed so they don't get re-added: PWA installability, Tailscale/remote access (D-014).
