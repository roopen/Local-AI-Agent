# Linux server deployment

AI News runs as one same-origin ASP.NET Core service: the .NET API serves the
React application, API routes, and the SignalR hub. The production container is
non-root, exposes only host loopback port 8080, and keeps its root filesystem
read-only. SQLite, invitations, passkeys, encrypted AI tokens, and ASP.NET data
protection keys live together in the `ainews-data` volume.

## Prerequisites

- Linux with a rootless Podman release that supports `.build` Quadlets
- A user systemd instance and a stable checkout at
  `~/src/Local-AI-Agent`
- A stable public HTTPS hostname and an existing reverse proxy
- The proxy's exact address as observed by the container and explicit trusted
  LAN CIDRs

WebAuthn credentials are scoped to the hostname. Choose the final public name
before registering the first passkey; changing `PUBLIC_ORIGIN` later requires
new passkeys.

## Install

From the repository checkout:

```sh
mkdir -p ~/.config/containers/systemd ~/.config/ainews
install -m 0644 deploy/quadlet/ainews.build ~/.config/containers/systemd/
install -m 0644 deploy/quadlet/ainews.container ~/.config/containers/systemd/
install -m 0644 deploy/quadlet/ainews.volume ~/.config/containers/systemd/
install -m 0600 deploy/quadlet/ainews.env.example ~/.config/ainews/ainews.env
```

Edit `~/.config/ainews/ainews.env` before starting. At minimum:

- Set `PUBLIC_ORIGIN` to the exact HTTPS origin, without a path or trailing
  hostname alias.
- Set `Security__TrustedProxies__0` to the reverse proxy source address seen by
  the application. Do not enter a public/client network here. If the proxy
  reaches a rootless port through a Podman gateway, use that exact gateway
  address rather than assuming `127.0.0.1`.
- Set one or more `Security__BootstrapAllowedNetworks__N` values to the LAN CIDRs
  that may create the first account.
- Optionally set the first AI endpoint/model shown to the owner. A model server
  on the Podman host is normally reached as
  `http://host.containers.internal:1234/v1/`.

The service refuses to start in production if `PUBLIC_ORIGIN` or the trusted
proxy list is missing. Forwarded headers from all other sources are ignored.

Enable lingering so the rootless user service survives logout and starts after
reboot (this one command is run by an administrator):

```sh
sudo loginctl enable-linger "$USER"
systemctl --user daemon-reload
systemctl --user enable --now ainews.service
```

The container unit references `ainews.build`, so systemd builds the local image
before starting the application. EF Core applies pending migrations once during
startup.

Useful commands:

```sh
systemctl --user status ainews.service
journalctl --user-unit ainews.service -f
systemctl --user restart ainews-build.service
systemctl --user restart ainews.service
podman healthcheck run ainews
curl --fail http://127.0.0.1:8080/alive
ss -ltn | grep 8080
```

Only `127.0.0.1:8080` should be listening. The public firewall must not expose
8080.

## Reverse proxy

`deploy/reverse-proxy/nginx.conf.example` contains an nginx example. Its `map`
belongs in nginx's `http` block and its `location` belongs in the HTTPS virtual
host. Configure the proxy to:

- terminate valid public TLS;
- overwrite `Host`, `X-Forwarded-Host`, `X-Forwarded-Proto`, and
  `X-Forwarded-For` instead of preserving client-supplied values;
- proxy only to `http://127.0.0.1:8080`; and
- allow HTTP/1.1 WebSocket upgrades, including `/newsHub`.

After reloading the proxy, open `PUBLIC_ORIGIN` from an allowed LAN. On an empty
database, that device can create the sole owner account. Public clients will see
only passkey login. Once the owner exists, all new registration requires an
invitation link created under Settings > Administration.

## Backup

A backup must contain the complete data volume, especially both the SQLite
database and data-protection keys. Without the keys, saved AI credentials cannot
be decrypted. Stopping the single service makes the volume export
SQLite-consistent:

```sh
backup_dir="$HOME/.local/share/ainews-backups"
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$backup_dir"
systemctl --user stop ainews.service
podman volume export ainews-data > "$backup_dir/ainews-$stamp.tar"
sha256sum "$backup_dir/ainews-$stamp.tar" > "$backup_dir/ainews-$stamp.tar.sha256"
systemctl --user start ainews.service
```

Store a copy off the server. Test restores periodically.

## Upgrade

Record the current source revision, make a consistent backup, update the
checkout, rebuild, and restart:

```sh
git rev-parse HEAD
systemctl --user stop ainews.service
podman volume export ainews-data > "$HOME/.local/share/ainews-backups/ainews-pre-upgrade.tar"
git pull --ff-only
systemctl --user daemon-reload
systemctl --user restart ainews-build.service
systemctl --user start ainews.service
systemctl --user status ainews.service
curl --fail http://127.0.0.1:8080/health
```

Review the journal after startup. Migrations run before the service accepts
requests; do not start two replicas against this SQLite volume.

## Restore and rollback

Restore replaces the entire named volume. Verify the archive checksum and keep a
pre-restore export until the restored service has been tested:

```sh
systemctl --user stop ainews.service
podman volume export ainews-data > "$HOME/.local/share/ainews-backups/ainews-pre-restore.tar"
podman volume rm ainews-data
systemctl --user restart ainews-volume.service
podman volume import ainews-data /path/to/ainews-backup.tar
systemctl --user start ainews.service
```

For an application rollback, stop the service, return the checkout to the
recorded release revision, rebuild the image, and restore the backup taken
before that upgrade. Restoring the matching data backup is required because EF
migrations are not automatically reversed by running an older image.

## Operational checks

- An uninvited public browser can only log in.
- The bootstrap screen appears only on an empty database and an allowed client
  network.
- Invitations expire after seven days and can be revoked before use.
- A disabled member loses access on its next request because sessions are
  checked against SQLite.
- Members never receive AI endpoint/model/token details and receive 403 from
  owner APIs.
- Passkey login and the news stream work through the public HTTPS URL.
- Rebuilding the image or rebooting the server preserves `/data`.
