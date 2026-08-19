# Linux server deployment

AI News runs as one same-origin ASP.NET Core service: the .NET API serves the
React application, API routes, and the SignalR hub. The production container is
non-root, exposes only host loopback port 8180, and keeps its root filesystem
read-only. SQLite, invitations, passkeys, encrypted AI tokens, and ASP.NET data
protection keys live together in the `ainews-data` volume.

## Prerequisites

- Linux with rootless Podman and Quadlet support
- A user systemd instance and a stable checkout at
  `~/src/Local-AI-Agent`
- A stable public HTTPS hostname and either an existing reverse proxy or
  Cloudflare Tunnel
- The proxy's exact address as observed by the container and explicit trusted
  LAN CIDRs

WebAuthn credentials are scoped to the hostname. Choose the final public name
before registering the first passkey; changing `PUBLIC_ORIGIN` later requires
new passkeys.

## Install

From the repository checkout:

```sh
mkdir -p ~/.config/containers/systemd ~/.config/ainews ~/.local/bin
install -m 0644 deploy/quadlet/ainews.build ~/.config/containers/systemd/
install -m 0644 deploy/quadlet/ainews.container ~/.config/containers/systemd/
install -m 0644 deploy/quadlet/ainews.volume ~/.config/containers/systemd/
install -m 0600 deploy/quadlet/ainews.env.example ~/.config/ainews/ainews.env
install -m 0755 deploy/update-ainews.sh ~/.local/bin/update-ainews
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
podman build --pull=newer --tag localhost/ainews:latest --file Containerfile .
systemctl --user daemon-reload
systemctl --user enable --now ainews.service
```

The application Quadlet consumes the explicit local image name
`localhost/ainews:latest`. The supplied `.build` unit is used by the update
script when the installed Podman can generate it; otherwise, the script runs
the equivalent `podman build` command directly. EF Core applies pending
migrations once during startup.

Useful commands:

```sh
systemctl --user status ainews.service
journalctl --user-unit ainews.service -f
systemctl --user restart ainews.service
podman healthcheck run ainews
curl --fail http://127.0.0.1:8180/alive
ss -ltn | grep 8180
```

Only `127.0.0.1:8180` should be listening. The public firewall must not expose
8180. Port 8080 remains internal to the container.

## Cloudflare Tunnel

Cloudflare Tunnel can be the HTTPS ingress instead of nginx. Run `cloudflared`
on the same Linux host and route the public hostname directly to
`http://127.0.0.1:8180`. The application port remains loopback-only, so the
tunnel uses an outbound connection and no inbound firewall port is required.
See Cloudflare's [published-application routing](https://developers.cloudflare.com/tunnel/routing/)
and [Linux service](https://developers.cloudflare.com/tunnel/advanced/local-management/as-a-service/linux/)
documentation for tunnel creation and installation.

Copy `deploy/cloudflare/config.yml.example` to the configuration used by your
locally managed tunnel, replace the tunnel UUID, credentials path, and both
hostname placeholders, then validate it:

```sh
cloudflared tunnel ingress validate
cloudflared tunnel route dns REPLACE_WITH_TUNNEL_UUID news.example.com
curl --fail http://127.0.0.1:8180/alive
```

Set these application values in `~/.config/ainews/ainews.env`:

```ini
PUBLIC_ORIGIN=https://news.example.com
Security__ForwardedForHeaderName=CF-Connecting-IP
Security__TrustedProxies__0=127.0.0.1
```

The trusted proxy value must be the address that the application actually sees
for the `cloudflared` connection. Depending on the rootless Podman network, that
may be its gateway rather than `127.0.0.1`; if forwarded-header logs report an
unknown proxy, replace the value with that exact address. Do not trust a broad
client or Cloudflare address range: only the local tunnel process can reach the
loopback-published port.

If the observed address is unclear, temporarily add
`Logging__LogLevel__Microsoft.AspNetCore.HttpOverrides=Debug` to the environment
file, restart `ainews.service`, make one request through the public hostname,
and inspect `journalctl --user-unit ainews.service`. Remove the logging override
after setting the exact proxy address.

Cloudflare Tunnel reports the visitor through `CF-Connecting-IP`. Selecting that
single-address header prevents a caller-supplied `X-Forwarded-For` chain from
affecting bootstrap checks or rate limiting. Keep Cloudflare's "Remove visitor
IP headers" transform disabled for this hostname. Enable WebSockets in the
Cloudflare zone so the SignalR `/newsHub` connection can upgrade normally.

The first owner request will carry the device's public egress address, not its
private LAN address, when it travels through Cloudflare. Add that exact address
as a `/32` (IPv4) or `/128` (IPv6) bootstrap network, or use split DNS with a
trusted local HTTPS proxy if private LAN CIDR matching is required. After an
owner exists, bootstrap registration is permanently closed regardless of this
setting.

Install and enable `cloudflared` as a service after validating the tunnel. A
dashboard-managed tunnel can use the same published application values:
hostname `news.example.com`, service `http://127.0.0.1:8180`, and HTTP Host
Header `news.example.com`.

For a locally managed tunnel whose configuration is in the service user's home:

```sh
sudo cloudflared --config "$HOME/.cloudflared/config.yml" service install
sudo systemctl enable --now cloudflared
sudo systemctl status cloudflared
```

## Reverse proxy

`deploy/reverse-proxy/nginx.conf.example` contains an nginx example. Its `map`
belongs in nginx's `http` block and its `location` belongs in the HTTPS virtual
host. Configure the proxy to:

- terminate valid public TLS;
- overwrite `Host`, `X-Forwarded-Host`, `X-Forwarded-Proto`, and
  `X-Forwarded-For` instead of preserving client-supplied values;
- proxy only to `http://127.0.0.1:8180`; and
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

The update script requires a clean checkout at `~/src/Local-AI-Agent`. It pulls
only fast-forward changes, refreshes the installed Quadlet definitions, creates
`localhost/ainews:latest`, reloads the user systemd manager, restarts
`ainews.service`, and waits for the health endpoint. It uses
`ainews-build.service` when available and automatically falls back to
`podman build` when that generated unit is unavailable:

```sh
~/.local/bin/update-ainews
```

For a migration-sensitive release, take a consistent backup first:

```sh
git rev-parse HEAD
systemctl --user stop ainews.service
podman volume export ainews-data > "$HOME/.local/share/ainews-backups/ainews-pre-upgrade.tar"
systemctl --user start ainews.service
~/.local/bin/update-ainews
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
