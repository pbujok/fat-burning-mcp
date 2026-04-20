#!/usr/bin/env bash
set -euo pipefail

if [[ $EUID -ne 0 ]]; then
  echo "Error: this script must be run as root (sudo bash deploy.sh)" >&2
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOLUTION_PATH="$SCRIPT_DIR/../.."
PUBLISH_DIR="/tmp/fat-burning-mcp-publish"
INSTALL_DIR="/opt/fat-burning-mcp"
SERVICE_NAME="fat-burning-mcp"
ENV_DIR="/etc/fat-burning-mcp"
ENV_FILE="$ENV_DIR/env"
SERVICE_FILE="/etc/systemd/system/$SERVICE_NAME.service"

echo "==> Publishing application..."
dotnet publish "$SOLUTION_PATH/FatBurningMcp.sln" \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -o "$PUBLISH_DIR"

echo "==> Creating system user..."
if ! id "$SERVICE_NAME" &>/dev/null; then
  useradd --system --no-create-home "$SERVICE_NAME"
fi

echo "==> Deploying files to $INSTALL_DIR..."
mkdir -p "$INSTALL_DIR"
rsync -a --delete "$PUBLISH_DIR/" "$INSTALL_DIR/"
chmod +x "$INSTALL_DIR/FatBurner.Mcp"
chown -R "$SERVICE_NAME": "$INSTALL_DIR"

echo "==> Configuring Strava credentials..."
if [[ ! -f "$ENV_FILE" ]]; then
  mkdir -p "$ENV_DIR"
  read -rp "Strava Client ID: " strava_client_id
  read -rsp "Strava Client Secret: " strava_client_secret; echo
  read -rsp "Strava Refresh Token: " strava_refresh_token; echo

  cat > "$ENV_FILE" <<EOF
STRAVA__ClientId=$strava_client_id
STRAVA__ClientSecret=$strava_client_secret
STRAVA__RefreshToken=$strava_refresh_token
EOF

  chown "root:$SERVICE_NAME" "$ENV_FILE"
  chmod 640 "$ENV_FILE"
  echo "Credentials written to $ENV_FILE"
else
  echo "Skipping credentials (file already exists). Edit $ENV_FILE to update."
fi

echo "==> Installing systemd service..."
cp "$SCRIPT_DIR/fat-burning-mcp.service" "$SERVICE_FILE"
systemctl daemon-reload

echo "==> Enabling and starting service..."
systemctl enable --now "$SERVICE_NAME"
systemctl restart "$SERVICE_NAME"

echo ""
echo "==> Deployment complete."
systemctl status "$SERVICE_NAME" --no-pager -l
