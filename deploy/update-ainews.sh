#!/usr/bin/env bash
set -Eeuo pipefail

readonly image_name="localhost/ainews:latest"
readonly service_name="ainews.service"
readonly health_url="http://127.0.0.1:8180/alive"
readonly quadlet_target_dir="/etc/containers/systemd"
readonly environment_file="/etc/ainews/ainews.env"
readonly system_bin_dir="/usr/local/sbin"

fail() {
    printf 'AI News update failed: %s\n' "$*" >&2
    exit 1
}

for command_name in git install podman systemctl curl stat journalctl id getent realpath runuser env rm grep sed; do
    command -v "${command_name}" >/dev/null 2>&1 \
        || fail "required command '${command_name}' was not found"
done

[[ "$(id -u)" -eq 0 ]] \
    || fail "run this system-wide updater as root, for example with sudo"

[[ "$#" -le 1 ]] || fail "usage: update-ainews [/absolute/path/to/Local-AI-Agent]"

repository_dir="${1:-}"
if [[ -z "${repository_dir}" ]]; then
    invoking_user="${SUDO_USER:-}"
    [[ -n "${invoking_user}" && "${invoking_user}" != "root" ]] \
        || fail "pass the absolute repository path when not invoking through sudo"

    invoking_user_record="$(getent passwd "${invoking_user}")" \
        || fail "could not resolve invoking user '${invoking_user}'"
    IFS=: read -r _ _ _ _ _ invoking_user_home _ <<< "${invoking_user_record}"
    repository_dir="${invoking_user_home}/Local-AI-Agent"
fi

[[ -d "${repository_dir}/.git" ]] \
    || fail "expected the checkout at ${repository_dir}"
repository_dir="$(realpath "${repository_dir}")"
readonly repository_dir
readonly quadlet_source_dir="${repository_dir}/deploy/quadlet"

[[ -f "${environment_file}" ]] \
    || fail "create ${environment_file} from deploy/quadlet/ainews.env.example first"

repository_owner_uid="$(stat -c '%u' "${repository_dir}")"
repository_owner_record="$(getent passwd "${repository_owner_uid}")" \
    || fail "could not resolve repository owner UID ${repository_owner_uid}"
IFS=: read -r repository_owner _ _ _ _ repository_owner_home _ <<< "${repository_owner_record}"

run_git() {
    if [[ "${repository_owner_uid}" -eq 0 ]]; then
        git -C "${repository_dir}" "$@"
    else
        runuser --user "${repository_owner}" -- \
            env HOME="${repository_owner_home}" git -C "${repository_dir}" "$@"
    fi
}

if ! run_git diff --quiet || ! run_git diff --cached --quiet; then
    fail "the checkout has tracked changes; commit or stash them before updating"
fi

printf 'Updating source in %s\n' "${repository_dir}"
run_git pull --ff-only

printf 'Installing current Quadlet definitions\n'
install -d -m 0755 "${quadlet_target_dir}"
install -m 0644 "${quadlet_source_dir}/ainews.container" "${quadlet_target_dir}/ainews.container"
install -m 0644 "${quadlet_source_dir}/ainews.volume" "${quadlet_target_dir}/ainews.volume"
for reader_unit in article-reader.network article-egress.network article-egress.container article-mcp.container; do
    # Preserve the host's Ubuntu/crun AppArmor workaround across deployments.
    preserve_no_new_privileges=false
    if [[ "${reader_unit}" == *.container ]] && [[ -f "${quadlet_target_dir}/${reader_unit}" ]] \
        && grep -Eq '^NoNewPrivileges=false[[:space:]]*$' "${quadlet_target_dir}/${reader_unit}"; then
        preserve_no_new_privileges=true
    fi
    install -m 0644 "${quadlet_source_dir}/${reader_unit}" "${quadlet_target_dir}/${reader_unit}"
    if [[ "${preserve_no_new_privileges}" == true ]]; then
        sed -i 's/^NoNewPrivileges=true/NoNewPrivileges=false/' "${quadlet_target_dir}/${reader_unit}"
    fi
done
# Keep the environment file consistent with the bundled static endpoint.
# Preserve custom remote endpoints.
sed -i 's|^ArticleReader__McpEndpoint=http://article-mcp:8931/mcp\r\?$|ArticleReader__McpEndpoint=http://10.203.0.3:8931/mcp|' "${environment_file}"
rm -f -- "${quadlet_target_dir}/ainews.build"
install -d -m 0755 "${system_bin_dir}"
install -m 0755 "${repository_dir}/deploy/update-ainews.sh" "${system_bin_dir}/update-ainews"

printf 'Reloading the system systemd manager\n'
systemctl daemon-reload

printf 'Building %s\n' "${image_name}"
podman build \
    --pull=newer \
    --tag "${image_name}" \
    --file "${repository_dir}/Containerfile" \
    "${repository_dir}"
podman image exists "${image_name}" \
    || fail "the build completed without creating ${image_name}"

printf 'Restarting %s\n' "${service_name}"
podman build --tag localhost/ainews-article-egress:1 \
    --file "${repository_dir}/deploy/article-reader/Proxy.Containerfile" "${repository_dir}"
podman build --tag localhost/ainews-article-mcp:0.0.80 \
    --file "${repository_dir}/deploy/article-reader/Mcp.Containerfile" "${repository_dir}"
if ! podman network exists article-reader-static; then
    # The old generated network service may still be active after daemon-reload.
    # Create the new fixed-subnet network without deleting the existing network.
    systemctl stop "${service_name}" article-mcp.service article-egress.service
    systemctl restart article-reader-network.service
fi
systemctl reset-failed article-egress.service article-mcp.service "${service_name}"
systemctl restart article-egress.service article-mcp.service
systemctl restart "${service_name}"

health_deadline=$((SECONDS + 90))
while ((SECONDS < health_deadline)); do
    if curl --fail --silent --max-time 2 "${health_url}" >/dev/null 2>&1; then
        printf 'AI News is healthy and running %s\n' "${image_name}"
        systemctl --no-pager --full status "${service_name}"
        exit 0
    fi
    sleep 2
done

systemctl --no-pager --full status "${service_name}" >&2 || true
journalctl --unit "${service_name}" --no-pager --lines 80 >&2 || true
fail "the service did not become healthy within 90 seconds"
