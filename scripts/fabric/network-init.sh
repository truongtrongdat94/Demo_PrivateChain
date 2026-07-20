#!/usr/bin/env bash
set -euo pipefail

readonly channel_name=hashchannel
readonly channel_block="/config/${channel_name}.block"
readonly orderer_admin="orderer.example.com:7053"
readonly orderer_tls_ca="/credentials/ordererOrganizations/example.com/orderers/orderer.example.com/tls/ca.crt"
readonly orderer_admin_tls_cert="/credentials/ordererOrganizations/example.com/users/Admin@example.com/tls/client.crt"
readonly orderer_admin_tls_key="/credentials/ordererOrganizations/example.com/users/Admin@example.com/tls/client.key"

retry() {
  local attempt=1
  local max_attempts=60
  until "$@"; do
    if (( attempt >= max_attempts )); then
      echo "Command failed after ${max_attempts} attempts: $*" >&2
      return 1
    fi
    attempt=$((attempt + 1))
    sleep 2
  done
}

if [[ ! -f "${channel_block}" ]]; then
  echo "Missing static channel block: ${channel_block}" >&2
  exit 1
fi

export FABRIC_CFG_PATH=/etc/hyperledger/fabric

orderer_has_channel() {
  osnadmin channel list \
    --channelID "${channel_name}" \
    -o "${orderer_admin}" \
    --ca-file "${orderer_tls_ca}" \
    --client-cert "${orderer_admin_tls_cert}" \
    --client-key "${orderer_admin_tls_key}" 2>/dev/null | grep -q "\"name\": \"${channel_name}\""
}

orderer_channel_is_active() {
  osnadmin channel list \
    --channelID "${channel_name}" \
    -o "${orderer_admin}" \
    --ca-file "${orderer_tls_ca}" \
    --client-cert "${orderer_admin_tls_cert}" \
    --client-key "${orderer_admin_tls_key}" 2>/dev/null | grep -q '"status": "active"'
}

orderer_is_ready() {
  osnadmin channel list \
    -o "${orderer_admin}" \
    --ca-file "${orderer_tls_ca}" \
    --client-cert "${orderer_admin_tls_cert}" \
    --client-key "${orderer_admin_tls_key}" >/dev/null 2>&1
}

retry orderer_is_ready

if ! orderer_has_channel; then
  osnadmin channel join \
    --channelID "${channel_name}" \
    --config-block "${channel_block}" \
    -o "${orderer_admin}" \
    --ca-file "${orderer_tls_ca}" \
    --client-cert "${orderer_admin_tls_cert}" \
    --client-key "${orderer_admin_tls_key}"
fi

retry orderer_channel_is_active

use_org() {
  local org="$1"
  export CORE_PEER_TLS_ENABLED=true
  export CORE_PEER_LOCALMSPID="${org}MSP"
  export CORE_PEER_MSPCONFIGPATH="/credentials/peerOrganizations/${org,,}.example.com/users/Admin@${org,,}.example.com/msp"
  export CORE_PEER_TLS_ROOTCERT_FILE="/credentials/peerOrganizations/${org,,}.example.com/peers/peer0.${org,,}.example.com/tls/ca.crt"
  export CORE_PEER_ADDRESS="peer0.${org,,}.example.com:7051"
}

join_peer() {
  local org="$1"
  use_org "${org}"
  if ! peer channel getinfo -c "${channel_name}" >/dev/null 2>&1; then
    peer channel join -b "${channel_block}"
  fi
  peer channel getinfo -c "${channel_name}" >/dev/null
}

retry join_peer Org1
retry join_peer Org2
