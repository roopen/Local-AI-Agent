#!/usr/bin/env bash
set -Eeuo pipefail

readonly image_name="localhost/ainews:latest"
readonly service_name="ainews.service"
readonly build_service_name="ainews-build.service"
readonly health_url="http://127.0.0.1:8180/alive"
readonly repository_dir="${HOME}/src/Local-AI-Agent"
readonly quadlet_source_dir="${repository_dir}/deploy/quadlet"
readonly config_home="${HOME}/.config"
readonly quadlet_target_dir="${config_home}/containers/systemd"
readonly environment_file="${config_home}/ainews/ainews.env"
readonly local_bin_dir="${HOME}/.local/bin"

fail() {
    printf 'AI News update failed: %s\n' "$*" >&2
    exit 1
}

for command_name in git install podman systemctl curl; do
    command -v "${command_name}" >/dev/null 2>&1 \
        || fail "required command '${command_name}' was not found"
done

[[ "$(id -u)" -ne 0 ]] \
    || fail "run this as the rootless AI News service user, not root"
[[ -d "${repository_dir}/.git" ]] \
    || fail "expected the checkout at ${repository_dir}"
[[ -f "${environment_file}" ]] \
    || fail "create ${environment_file} from deploy/quadlet/ainews.env.example first"

if ! git -C "${repository_dir}" diff --quiet \
    || ! git -C "${repository_dir}" diff --cached --quiet; then
    fail "the checkout has tracked changes; commit or stash them before updating"
fi

printf 'Updating source in %s\n' "${repository_dir}"
git -C "${repository_dir}" pull --ff-only

printf 'Installing current Quadlet definitions\n'
install -d -m 0755 "${quadlet_target_dir}"
install -m 0644 "${quadlet_source_dir}/ainews.build" "${quadlet_target_dir}/ainews.build"
install -m 0644 "${quadlet_source_dir}/ainews.container" "${quadlet_target_dir}/ainews.container"
install -m 0644 "${quadlet_source_dir}/ainews.volume" "${quadlet_target_dir}/ainews.volume"
install -d -m 0755 "${local_bin_dir}"
install -m 0755 "${repository_dir}/deploy/update-ainews.sh" "${local_bin_dir}/update-ainews"

printf 'Reloading the user systemd manager\n'
systemctl --user daemon-reload

printf 'Building %s\n' "${image_name}"
systemctl --user restart "${build_service_name}"
podman image exists "${image_name}" \
    || fail "the Quadlet build completed without creating ${image_name}"

printf 'Restarting %s\n' "${service_name}"
systemctl --user restart "${service_name}"

health_deadline=$((SECONDS + 90))
while ((SECONDS < health_deadline)); do
    if curl --fail --silent --max-time 2 "${health_url}" >/dev/null 2>&1; then
        printf 'AI News is healthy and running %s\n' "${image_name}"
        systemctl --user --no-pager --full status "${service_name}"
        exit 0
    fi
    sleep 2
done

systemctl --user --no-pager --full status "${service_name}" >&2 || true
journalctl --user-unit "${service_name}" --no-pager --lines 80 >&2 || true
fail "the service did not become healthy within 90 seconds"
