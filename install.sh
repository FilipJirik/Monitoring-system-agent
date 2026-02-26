#!/usr/bin/env bash
# ============================================================================
# Monitoring System Client Agent — Installation Script
# Installs the agent binary and systemd service file.
# Usage: sudo bash install.sh
# ============================================================================

set -euo pipefail

# ---- Configuration ---------------------------------------------------------
INSTALL_DIR="/opt/monitoring-agent"
SERVICE_NAME="monitoring-agent"
BINARY_NAME="monitoring-agent"
SERVICE_FILE="${SERVICE_NAME}.service"
SYSTEMD_DIR="/etc/systemd/system"

# ---- Colors ----------------------------------------------------------------
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

info()  { echo -e "${GREEN}[INFO]${NC}  $1"; }
warn()  { echo -e "${YELLOW}[WARN]${NC}  $1"; }
error() { echo -e "${RED}[ERROR]${NC} $1"; exit 1; }

# ---- Preflight checks ------------------------------------------------------
if [[ $EUID -ne 0 ]]; then
    error "This script must be run as root. Use: sudo bash install.sh"
fi

if [[ ! -f "./${BINARY_NAME}" ]]; then
    error "Binary '${BINARY_NAME}' not found in the current directory."
fi

# ---- Generate service file if not present -----------------------------------
if [[ ! -f "./${SERVICE_FILE}" ]]; then
    info "Generating ${SERVICE_FILE}..."
    cat > "./${SERVICE_FILE}" <<EOF
[Unit]
Description=Monitoring System Client Agent
After=network-online.target
Wants=network-online.target

[Service]
Type=notify
ExecStart=${INSTALL_DIR}/${BINARY_NAME}
WorkingDirectory=${INSTALL_DIR}
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
EOF
fi

# ---- Stop existing service if running ---------------------------------------
if systemctl is-active --quiet "${SERVICE_NAME}.service" 2>/dev/null; then
    warn "Stopping existing ${SERVICE_NAME} service..."
    systemctl stop "${SERVICE_NAME}.service"
fi

# ---- Install ----------------------------------------------------------------
info "Creating install directory: ${INSTALL_DIR}"
mkdir -p "${INSTALL_DIR}"

info "Copying binary to ${INSTALL_DIR}/${BINARY_NAME}"
cp "./${BINARY_NAME}" "${INSTALL_DIR}/${BINARY_NAME}"
chmod +x "${INSTALL_DIR}/${BINARY_NAME}"

# Copy existing config if present
if [[ -f "./agent_config.toml" ]]; then
    info "Copying existing configuration file..."
    cp "./agent_config.toml" "${INSTALL_DIR}/agent_config.toml"
fi

info "Installing systemd service to ${SYSTEMD_DIR}/${SERVICE_FILE}"
cp "./${SERVICE_FILE}" "${SYSTEMD_DIR}/${SERVICE_FILE}"

# ---- Enable & start --------------------------------------------------------
info "Reloading systemd daemon..."
systemctl daemon-reload

info "Enabling ${SERVICE_NAME} service..."
systemctl enable "${SERVICE_NAME}.service"

# ---- Check if configuration exists -----------------------------------------
if [[ -f "${INSTALL_DIR}/agent_config.toml" ]] && grep -q "device_id" "${INSTALL_DIR}/agent_config.toml" 2>/dev/null; then
    info "Starting ${SERVICE_NAME} service..."
    systemctl start "${SERVICE_NAME}.service"

    echo ""
    info "Installation complete! The agent is now running."
    echo ""
    echo "  Check status:  sudo systemctl status ${SERVICE_NAME}.service"
    echo "  View logs:     sudo journalctl -u ${SERVICE_NAME}.service -f"
else
    echo ""
    info "Installation complete! Binary and service installed."
    warn "No configuration found. Run setup before starting the service:"
    echo ""
    echo "  cd ${INSTALL_DIR}"
    echo "  sudo ./${BINARY_NAME} register \\"
    echo "    --server-url=https://your-server.com \\"
    echo "    --email=your@email.com \\"
    echo "    --password=yourpassword \\"
    echo "    --device-name=\"My Server\""
    echo ""
    echo "  Then start the service:"
    echo "  sudo systemctl start ${SERVICE_NAME}.service"
fi

echo ""
