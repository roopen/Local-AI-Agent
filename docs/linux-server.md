# Linux server deployment

AI News runs as one same-origin ASP.NET Core system service: the .NET API serves
the React application, API routes, and the SignalR hub. Podman and systemd
manage it system-wide, while the process inside the production container still
runs as a non-root user. It exposes only host loopback port 8180 and keeps its
root filesystem read-only. SQLite, invitations, passkeys, encrypted AI tokens,
and ASP.NET data-protection keys live together in the `ainews-data` volume.

## Prerequisites

- Linux with rootful Podman, systemd, and Quadlet support
- A stable checkout at `~/Local-AI-Agent`, owned by the trusted administrator
  who performs updates
- A stable public HTTPS hostname and either an existing reverse proxy or
  Cloudflare Tunnel
- The proxy's exact address as observed by the container

WebAuthn credentials are scoped to the hostname. Choose the final public name
before registering the first passkey; changing `PUBLIC_ORIGIN` later requires
new passkeys.

## Install

From the existing repository checkout, install the system units and
configuration:

```sh
cd ~/Local-AI-Agent
sudo install -d -m 0755 /etc/containers/systemd /usr/local/sbin
sudo install -d -m 0750 /etc/ainews
sudo install -m 0644 deploy/quadlet/ainews.container /etc/containers/systemd/
sudo install -m 0644 deploy/quadlet/ainews.volume /etc/containers/systemd/
sudo test -f /etc/ainews/ainews.env \
  || sudo install -m 0600 deploy/quadlet/ainews.env.example /etc/ainews/ainews.env
sudo install -m 0755 deploy/update-ainews.sh /usr/local/sbin/update-ainews
```

Edit `/etc/ainews/ainews.env` before starting. At minimum:

- Set `PUBLIC_ORIGIN` to the exact HTTPS origin, without a path or trailing
  hostname alias.
- Replace `REPLACE_WITH_PODMAN_GATEWAY` in `Security__TrustedProxies__0` with
  the rootful Podman gateway reported by this command:

  ```sh
  sudo podman network inspect podman \
    --format '{{range .Subnets}}{{.Gateway}}{{end}}'
  ```

  This must be the source address seen by the application, not a public/client
  network. Host-published traffic crosses Podman's bridge, so the application
  does not normally see the host reverse proxy as `127.0.0.1`.
- Optionally set the first AI endpoint/model shown to the owner. A model server
  on the Podman host is normally reached as
  `http://host.containers.internal:1234/v1/`.

The service refuses to start in production if `PUBLIC_ORIGIN` or the trusted
proxy list is missing. Forwarded headers from all other sources are ignored.

If a previous rootless deployment already contains accounts or settings, export
its volume as the original service user before starting the system service:

```sh
systemctl --user stop ainews.service
umask 077
podman volume export ainews-data > "$HOME/ainews-data-rootless.tar"
```

After installing the system Quadlets, import that archive into the separate
rootful volume:

```sh
sudo systemctl daemon-reload
sudo systemctl start ainews-volume.service
sudo podman volume import ainews-data "$HOME/ainews-data-rootless.tar"
```

Keep the protected archive until the system service and encrypted AI settings
have been verified. Rootless and rootful Podman have separate image and volume
stores, so this import is required when retaining existing data.

Build the first image, reload the system manager, and enable the service:

```sh
sudo podman build --pull=newer --tag localhost/ainews:latest --file Containerfile .
sudo systemctl daemon-reload
sudo systemctl start ainews.service
```

Quadlet services are generated under `/run/systemd/generator` and cannot be
enabled with `systemctl enable`. The persistent source unit declares
`WantedBy=multi-user.target`, so the generator wires it into normal system boot
whenever systemd reloads.

The application Quadlet consumes the explicit local image name
`localhost/ainews:latest`. The updater builds that image directly from the
checkout before restarting the service. EF Core applies pending migrations once
during startup.

Useful commands:

```sh
sudo systemctl status ainews.service
sudo journalctl --unit ainews.service -f
sudo systemctl restart ainews.service
sudo podman healthcheck run ainews
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

Copy `deploy/cloudflare/config.yml.example` to `/etc/cloudflared/config.yml`,
copy the tunnel credentials JSON to the path named there, replace the tunnel
UUID and both hostname placeholders, then validate it:

```sh
sudo install -d -m 0700 /etc/cloudflared
sudo install -m 0600 deploy/cloudflare/config.yml.example /etc/cloudflared/config.yml
sudo install -m 0600 ~/.cloudflared/REPLACE_WITH_TUNNEL_UUID.json /etc/cloudflared/
sudo cloudflared tunnel --config /etc/cloudflared/config.yml ingress validate
cloudflared tunnel route dns REPLACE_WITH_TUNNEL_UUID news.example.com
curl --fail http://127.0.0.1:8180/alive
```

Set these application values in `/etc/ainews/ainews.env`:

```ini
PUBLIC_ORIGIN=https://news.example.com
Security__ForwardedForHeaderName=CF-Connecting-IP
Security__TrustedProxies__0=REPLACE_WITH_PODMAN_GATEWAY
```

The trusted proxy value must be the address that the application actually sees
for the `cloudflared` connection. With the supplied Quadlet, this is the Podman
gateway rather than `127.0.0.1`. After the container starts, confirm the exact
value with:

```sh
sudo podman inspect ainews \
  --format '{{range .NetworkSettings.Networks}}{{.Gateway}}{{end}}'
```

If forwarded-header logs report an unknown proxy, replace the value with that
exact address. Do not trust a broad client or Cloudflare address range: only the
local tunnel process can reach the loopback-published port.

If the observed address is unclear, temporarily add
`Logging__LogLevel__Microsoft.AspNetCore.HttpOverrides=Debug` to the environment
file, restart `ainews.service`, make one request through the public hostname,
and inspect `sudo journalctl --unit ainews.service`. Remove the logging override
after setting the exact proxy address.

Cloudflare Tunnel reports the visitor through `CF-Connecting-IP`. Selecting that
single-address header prevents a caller-supplied `X-Forwarded-For` chain from
affecting rate limiting. Keep Cloudflare's "Remove visitor IP headers" transform
disabled for this hostname. Enable WebSockets in the Cloudflare zone so the
SignalR `/newsHub` connection can upgrade normally.

Verify that HTTPS forwarding is active. The unauthenticated CSRF endpoint
redirect must retain the public `https` scheme:

```sh
curl --silent --show-error --head https://news.example.com/api/auth/csrf \
  | grep --ignore-case '^location:'
```

Install and enable `cloudflared` as a service after validating the tunnel. A
dashboard-managed tunnel can use the same published application values:
hostname `news.example.com`, service `http://127.0.0.1:8180`, and HTTP Host
Header `news.example.com`.

For the locally managed system tunnel:

```sh
sudo cloudflared --config /etc/cloudflared/config.yml service install
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

After reloading the proxy, open `PUBLIC_ORIGIN`. On an empty database, the
registration form creates the sole owner account. Once the owner exists, public
clients see only passkey login and all new registration requires an invitation
link created under Settings > Administration.

## Backup

A backup must contain the complete data volume, especially both the SQLite
database and data-protection keys. Without the keys, saved AI credentials cannot
be decrypted. Stopping the single service makes the volume export
SQLite-consistent:

```sh
backup_dir="/var/backups/ainews"
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup_file="$backup_dir/ainews-$stamp.tar"
sudo install -d -m 0700 "$backup_dir"
sudo systemctl stop ainews.service
sudo podman volume export ainews-data | sudo tee "$backup_file" >/dev/null
sudo sha256sum "$backup_file" | sudo tee "$backup_file.sha256" >/dev/null
sudo systemctl start ainews.service
```

Store a copy off the server. Test restores periodically.

## Upgrade

The update script requires a clean checkout at `~/Local-AI-Agent`. When invoked
through `sudo`, it discovers the invoking user's home, runs `git pull
--ff-only` as that user so their Git credentials continue to work, installs the
system Quadlets, builds `localhost/ainews:latest` with rootful Podman, reloads
systemd, restarts `ainews.service`, and waits for the health endpoint:

```sh
sudo /usr/local/sbin/update-ainews
```

For a root login or automation without `SUDO_USER`, pass the absolute checkout
path explicitly:

```sh
sudo /usr/local/sbin/update-ainews /home/REPLACE_WITH_USER/Local-AI-Agent
```

Only a trusted administrator should be able to modify this checkout: the
updater builds its Containerfile and installs its Quadlet definitions as root.

For a migration-sensitive release, take a consistent backup first:

```sh
git -C "$HOME/Local-AI-Agent" rev-parse HEAD
sudo install -d -m 0700 /var/backups/ainews
sudo systemctl stop ainews.service
sudo podman volume export ainews-data \
  | sudo tee /var/backups/ainews/ainews-pre-upgrade.tar >/dev/null
sudo systemctl start ainews.service
sudo /usr/local/sbin/update-ainews
```

Review the journal after startup. Migrations run before the service accepts
requests; do not start two replicas against this SQLite volume.

## Restore and rollback

Restore replaces the entire named volume. Verify the archive checksum and keep a
pre-restore export until the restored service has been tested:

```sh
sudo systemctl stop ainews.service
sudo podman volume export ainews-data \
  | sudo tee /var/backups/ainews/ainews-pre-restore.tar >/dev/null
sudo podman volume rm ainews-data
sudo systemctl restart ainews-volume.service
sudo podman volume import ainews-data /path/to/ainews-backup.tar
sudo systemctl start ainews.service
```

For an application rollback, stop the service, return the checkout to the
recorded release revision, rebuild the image, and restore the backup taken
before that upgrade. Restoring the matching data backup is required because EF
migrations are not automatically reversed by running an older image.

## Operational checks

- An empty installation offers owner registration to the first visitor.
- After the owner is created, an uninvited public browser can only log in.
- Invitations expire after seven days and can be revoked before use.
- A disabled member loses access on its next request because sessions are
  checked against SQLite.
- Members never receive AI endpoint/model/token details and receive 403 from
  owner APIs.
- Passkey login and the news stream work through the public HTTPS URL.
- Rebuilding the image or rebooting the server preserves `/data`.
