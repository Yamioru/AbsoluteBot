#!/bin/sh
# compose_host_build
# Watcher on the HOST. Must have Unix LF line endings, not Windows CRLF.
# GitHub login/token only in /opt/absolutebot/scripts/github.env, not in this file.

COMPOSE_DIR="${UPDATE_COMPOSE_DIR:-/opt/absolutebot}"
FLAG="${UPDATE_FLAG:-$COMPOSE_DIR/data/update.requested}"
LOCK="${UPDATE_LOCK:-$COMPOSE_DIR/data/update.lock}"
INTERVAL="${UPDATE_POLL_SECONDS:-5}"
HOME="${HOME:-/root}"
export HOME

if [ -n "${UPDATE_GIT_DIR:-}" ]; then
  GIT_DIR="$UPDATE_GIT_DIR"
elif [ -d "$COMPOSE_DIR/.git" ]; then
  GIT_DIR="$COMPOSE_DIR"
elif [ -d "$COMPOSE_DIR/AbsoluteBot/.git" ]; then
  GIT_DIR="$COMPOSE_DIR/AbsoluteBot"
else
  GIT_DIR="$COMPOSE_DIR"
fi

ENV_FILE="$COMPOSE_DIR/scripts/github.env"
ASKPASS="$COMPOSE_DIR/scripts/git-askpass.sh"
if [ -f "$ENV_FILE" ]; then
  # shellcheck disable=SC1090
  . "$ENV_FILE"
fi
if [ -n "${GITHUB_USER:-}" ] && [ -n "${GITHUB_TOKEN:-}" ] && [ -f "$ASKPASS" ]; then
  chmod +x "$ASKPASS" 2>/dev/null || true
  export GIT_ASKPASS_USER="$GITHUB_USER"
  export GIT_ASKPASS_PASSWORD="$GITHUB_TOKEN"
  export GIT_ASKPASS="$ASKPASS"
  export GIT_TERMINAL_PROMPT=0
fi

echo "AbsoluteBot update watcher: flag=$FLAG git=$GIT_DIR compose=$COMPOSE_DIR"

git_pull() {
  if [ -n "${GIT_ASKPASS:-}" ]; then
    git -C "$GIT_DIR" -c credential.helper= pull --ff-only
  else
    git -C "$GIT_DIR" \
      -c credential.helper= \
      -c "credential.helper=store --file=$HOME/.git-credentials" \
      pull --ff-only
  fi
}

strip_cr() {
  for f in "$@"; do
    if [ -f "$f" ]; then
      sed -i 's/\r$//' "$f" 2>/dev/null || true
    fi
  done
}

# Dockerfile COPY AbsoluteBot/ needs the compose host as context (clone is the AbsoluteBot/ folder).
resolve_build_dir() {
  if [ -f "$COMPOSE_DIR/Dockerfile" ] && [ -d "$COMPOSE_DIR/AbsoluteBot" ]; then
    echo "$COMPOSE_DIR"
    return
  fi
  if [ -f "$GIT_DIR/Dockerfile" ]; then
    echo "$GIT_DIR"
  elif [ -f "$GIT_DIR/AbsoluteBot/Dockerfile" ]; then
    echo "$GIT_DIR/AbsoluteBot"
  elif [ -f "$COMPOSE_DIR/Dockerfile" ]; then
    echo "$COMPOSE_DIR"
  else
    echo ""
  fi
}

# Keep host Debian Dockerfile. Do not replace it with Alpine from git.
protect_host_debian_dockerfile() {
  host_df="$COMPOSE_DIR/Dockerfile"
  git_df="$GIT_DIR/Dockerfile"
  if [ -f "$git_df" ] && grep -qi bookworm "$git_df"; then
    cp "$git_df" "$host_df"
    strip_cr "$host_df"
    echo "Host Dockerfile updated from git (Debian)."
  elif [ -f "$host_df" ] && grep -qi bookworm "$host_df"; then
    echo "Keeping host Debian Dockerfile (git is not Debian)."
  fi
}

install_host_watcher() {
  src="$GIT_DIR/scripts/watch-update.sh"
  dest="$COMPOSE_DIR/scripts/watch-update.sh"
  if [ ! -f "$src" ]; then
    return
  fi
  if ! grep -q compose_host_build "$src" 2>/dev/null; then
    echo "Git watcher has no compose_host_build marker; keeping host watch-update.sh."
    return
  fi
  mkdir -p "$COMPOSE_DIR/scripts"
  cp "$src" "$dest"
  strip_cr "$dest"
  chmod +x "$dest"
  echo "Installed host watch-update.sh from git."
}

while true; do
  if [ -f "$FLAG" ]; then
    rm -f "$FLAG"
    if mkdir "$LOCK" 2>/dev/null; then
      echo "Updating AbsoluteBot..."
      if git_pull; then
        echo "git HEAD: $(git -C "$GIT_DIR" log -1 --oneline 2>/dev/null || echo unknown)"
        CATALOG_SRC=""
        if [ -f "$GIT_DIR/BeginData/achievements.json" ]; then
          CATALOG_SRC="$GIT_DIR/BeginData/achievements.json"
        elif [ -f "$GIT_DIR/AbsoluteBot/BeginData/achievements.json" ]; then
          CATALOG_SRC="$GIT_DIR/AbsoluteBot/BeginData/achievements.json"
        fi
        if [ -n "$CATALOG_SRC" ]; then
          mkdir -p "$COMPOSE_DIR/data"
          cp "$CATALOG_SRC" "$COMPOSE_DIR/data/achievements.json"
        fi
        install_host_watcher
        protect_host_debian_dockerfile
        export ABS_DATA_DIR="$COMPOSE_DIR/data"
        BUILD_DIR="$(resolve_build_dir)"
        if [ -z "$BUILD_DIR" ]; then
          echo "Update failed: Dockerfile not found (git=$GIT_DIR compose=$COMPOSE_DIR)." >&2
          ls -la "$GIT_DIR" >&2 || true
        else
          COMPOSE_FILE="$COMPOSE_DIR/docker-compose.yml"
          if [ ! -f "$COMPOSE_FILE" ]; then
            COMPOSE_FILE="$BUILD_DIR/docker-compose.yml"
          fi
          strip_cr "$COMPOSE_FILE" "$BUILD_DIR/Dockerfile"
          echo "Building from BUILD_DIR=$BUILD_DIR compose=$COMPOSE_FILE"
          ls -l "$BUILD_DIR/Dockerfile" "$COMPOSE_FILE" || true
          BUILD_FLAGS="--pull"
          if docker exec absolutebot cat /etc/os-release 2>/dev/null | grep -qi alpine; then
            echo "Current image is Alpine; ONNX needs glibc - rebuilding without cache (Debian)."
            BUILD_FLAGS="--pull --no-cache"
          fi
          if docker compose --project-name absolutebot \
              --project-directory "$BUILD_DIR" \
              -f "$COMPOSE_FILE" \
              build $BUILD_FLAGS \
            && docker compose --project-name absolutebot \
              --project-directory "$BUILD_DIR" \
              -f "$COMPOSE_FILE" \
              up -d; then
            echo "Update finished."
          else
            echo "Update failed." >&2
          fi
        fi
      else
        echo "Update failed: git pull." >&2
      fi
      rmdir "$LOCK" 2>/dev/null || true
    else
      echo "Update already in progress, skip."
    fi
  fi
  sleep "$INTERVAL"
done
