const POLL_INTERVAL_MS = 5000;
const RECORD_LIMIT = 200;
const stationColors = ["#087f73", "#1976a3", "#a86100", "#6a5aa3", "#b34449", "#28734c"];

const metricDefinitions = {
  ph: { color: "#087f73", decimals: 2 },
  cod: { color: "#1976a3", decimals: 2 },
  bod: { color: "#6a5aa3", decimals: 2 },
  tss: { color: "#a86100", decimals: 2 },
  flow: { color: "#28734c", decimals: 2 },
  temperature: { color: "#b34449", decimals: 2 }
};

const elements = {
  stationFilter: document.querySelector("#station-filter"),
  statusFilter: document.querySelector("#status-filter"),
  refreshButton: document.querySelector("#refresh-button"),
  loadMoreButton: document.querySelector("#load-more-button"),
  verifyAllButton: document.querySelector("#verify-all-button"),
  connectionStatus: document.querySelector("#connection-status"),
  recordCount: document.querySelector("#record-count"),
  pendingCount: document.querySelector("#pending-count"),
  anchoredCount: document.querySelector("#anchored-count"),
  lastUpdated: document.querySelector("#last-updated"),
  chartScope: document.querySelector("#chart-scope"),
  recordsBody: document.querySelector("#records-body"),
  emptyState: document.querySelector("#empty-state"),
  toast: document.querySelector("#toast")
};

const verificationByRecord = new Map();
let verifyAllInFlight = false;
let records = [];
let nextBeforeId = null;
let refreshInFlight = false;
let loadMoreInFlight = false;
let stationRefreshCounter = 0;
let toastTimer;

async function fetchJson(url) {
  const response = await fetch(url, { headers: { Accept: "application/json" } });
  if (!response.ok) {
    let message = `YÃªu cáº§u tháº¥t báº¡i (${response.status}).`;
    try {
      const error = await response.json();
      message = error.message || message;
    } catch {
      // Response body is not JSON; keep the HTTP status message.
    }
    throw new Error(message);
  }

  return response.json();
}

async function loadStations() {
  const selectedStation = elements.stationFilter.value;
  const stationIds = await fetchJson("/api/stations");

  elements.stationFilter.replaceChildren(createOption("", "Táº¥t cáº£ tráº¡m"));
  for (const stationId of stationIds) {
    elements.stationFilter.append(createOption(stationId, stationId));
  }

  if (stationIds.includes(selectedStation)) {
    elements.stationFilter.value = selectedStation;
  }
}

function createOption(value, label) {
  const option = document.createElement("option");
  option.value = value;
  option.textContent = label;
  return option;
}

async function refreshDashboard({ refreshStations = false } = {}) {
  if (refreshInFlight) return;

  refreshInFlight = true;
  elements.refreshButton.disabled = true;
  try {
    if (refreshStations || stationRefreshCounter % 12 === 0) {
      await loadStations();
    }
    stationRefreshCounter += 1;

    const page = await fetchJson(`/api/records?${createRecordParameters()}`);
    records = page.records;
    nextBeforeId = page.nextBeforeId;
    renderDashboard();
    setConnectionState(true);
    elements.lastUpdated.textContent = new Date().toLocaleTimeString("vi-VN");
  } catch (error) {
    setConnectionState(false);
    showToast(error instanceof Error ? error.message : "KhÃ´ng táº£i Ä‘Æ°á»£c dá»¯ liá»‡u.");
  } finally {
    refreshInFlight = false;
    elements.refreshButton.disabled = false;
  }
}

function createRecordParameters(beforeId) {
  const parameters = new URLSearchParams({ limit: String(RECORD_LIMIT) });
  if (elements.stationFilter.value) {
    parameters.set("stationId", elements.stationFilter.value);
  }
  if (elements.statusFilter.value) {
    parameters.set("status", elements.statusFilter.value);
  }
  if (beforeId) {
    parameters.set("beforeId", String(beforeId));
  }
  return parameters;
}

async function loadMoreRecords() {
  if (loadMoreInFlight || !nextBeforeId) return;

  loadMoreInFlight = true;
  updateLoadMoreButton();
  try {
    const page = await fetchJson(`/api/records?${createRecordParameters(nextBeforeId)}`);
    records = records.concat(page.records);
    nextBeforeId = page.nextBeforeId;
    renderDashboard();
  } catch (error) {
    showToast(error instanceof Error ? error.message : "Khong tai them duoc du lieu.");
  } finally {
    loadMoreInFlight = false;
    updateLoadMoreButton();
  }
}

function renderDashboard() {
  elements.recordCount.textContent = String(records.length);
  elements.pendingCount.textContent = String(records.filter(record => record.status === "pending").length);
  elements.anchoredCount.textContent = String(records.filter(record => record.status === "anchored").length);
  elements.chartScope.textContent = elements.stationFilter.value || "Táº¥t cáº£ tráº¡m";
  updateVerifyAllButton();
  updateLoadMoreButton();
  renderCharts();
  renderTable();
}

function updateLoadMoreButton() {
  elements.loadMoreButton.hidden = !nextBeforeId;
  elements.loadMoreButton.disabled = loadMoreInFlight;
  elements.loadMoreButton.textContent = loadMoreInFlight ? "Đang tải..." : "Tải thêm";
}

function renderCharts() {
  for (const canvas of document.querySelectorAll("canvas[data-metric]")) {
    drawLineChart(canvas, canvas.dataset.metric);
  }
}

function drawLineChart(canvas, metric) {
  const definition = metricDefinitions[metric];
  const points = records
    .filter(record => record.observedAt && record.readings && Number.isFinite(Number(record.readings[metric])))
    .map(record => ({
      stationId: record.stationId || "KhÃ´ng rÃµ tráº¡m",
      timestamp: new Date(record.observedAt).getTime(),
      value: Number(record.readings[metric])
    }))
    .sort((left, right) => left.timestamp - right.timestamp);

  const bounds = canvas.getBoundingClientRect();
  const width = Math.max(240, Math.floor(bounds.width));
  const height = Math.max(140, Math.floor(bounds.height));
  const ratio = window.devicePixelRatio || 1;
  canvas.width = width * ratio;
  canvas.height = height * ratio;

  const context = canvas.getContext("2d");
  context.setTransform(ratio, 0, 0, ratio, 0, 0);
  context.clearRect(0, 0, width, height);

  if (points.length === 0) {
    context.fillStyle = "#78888f";
    context.font = "12px system-ui";
    context.textAlign = "center";
    context.fillText("ChÆ°a cÃ³ dá»¯ liá»‡u", width / 2, height / 2);
    return;
  }

  const padding = { top: 12, right: 10, bottom: 24, left: 44 };
  const values = points.map(point => point.value);
  const minimum = Math.min(...values);
  const maximum = Math.max(...values);
  const spread = maximum - minimum || Math.max(Math.abs(maximum) * 0.1, 1);
  const floor = minimum - spread * 0.12;
  const ceiling = maximum + spread * 0.12;
  const plotWidth = width - padding.left - padding.right;
  const plotHeight = height - padding.top - padding.bottom;
  const firstTimestamp = points[0].timestamp;
  const lastTimestamp = points.at(-1).timestamp;
  const timeSpread = lastTimestamp - firstTimestamp;

  context.strokeStyle = "#e1e8eb";
  context.lineWidth = 1;
  for (let line = 0; line <= 3; line += 1) {
    const y = padding.top + (plotHeight * line) / 3;
    context.beginPath();
    context.moveTo(padding.left, y);
    context.lineTo(width - padding.right, y);
    context.stroke();
  }

  context.fillStyle = "#72828a";
  context.font = "10px system-ui";
  context.textAlign = "right";
  context.fillText(ceiling.toFixed(definition.decimals), padding.left - 6, padding.top + 4);
  context.fillText(floor.toFixed(definition.decimals), padding.left - 6, padding.top + plotHeight);

  const stationGroups = new Map();
  for (const point of points) {
    const stationPoints = stationGroups.get(point.stationId) || [];
    stationPoints.push(point);
    stationGroups.set(point.stationId, stationPoints);
  }
  let stationIndex = 0;
  for (const [stationId, stationPoints] of stationGroups) {
    const color = stationGroups.size === 1
      ? definition.color
      : stationColors[stationIndex % stationColors.length];
    context.strokeStyle = color;
    context.lineWidth = 2;
    context.lineJoin = "round";
    context.lineCap = "round";
    context.beginPath();
    stationPoints.forEach((point, index) => {
      const x = padding.left + (timeSpread === 0
        ? plotWidth / 2
        : ((point.timestamp - firstTimestamp) / timeSpread) * plotWidth);
      const y = padding.top + plotHeight - ((point.value - floor) / (ceiling - floor)) * plotHeight;
      if (index === 0) context.moveTo(x, y);
      else context.lineTo(x, y);
    });
    context.stroke();

    if (stationGroups.size > 1) {
      const lastPoint = stationPoints.at(-1);
      const labelX = padding.left + (timeSpread === 0
        ? plotWidth / 2
        : ((lastPoint.timestamp - firstTimestamp) / timeSpread) * plotWidth);
      const labelY = padding.top + plotHeight - ((lastPoint.value - floor) / (ceiling - floor)) * plotHeight;
      context.fillStyle = color;
      context.font = "10px system-ui";
      context.textAlign = "right";
      context.fillText(stationId, Math.min(labelX, width - padding.right), Math.max(10, labelY - 5));
    }
    stationIndex += 1;
  }

  const firstTime = new Date(firstTimestamp).toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
  const lastTime = new Date(lastTimestamp).toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
  context.fillStyle = "#72828a";
  context.textAlign = "left";
  context.fillText(firstTime, padding.left, height - 5);
  context.textAlign = "right";
  context.fillText(lastTime, width - padding.right, height - 5);
}

function renderTable() {
  elements.recordsBody.replaceChildren();
  elements.emptyState.hidden = records.length !== 0;

  for (const record of records) {
    const row = document.createElement("tr");
    appendTextCell(row, record.id);
    appendTextCell(row, record.stationId ?? "â€”");
    appendTextCell(row, formatDateTime(record.observedAt));
    appendTextCell(row, formatReading(record.readings?.ph));
    appendTextCell(row, formatReading(record.readings?.cod));
    appendTextCell(row, formatReading(record.readings?.bod));
    appendTextCell(row, formatReading(record.readings?.tss));
    appendTextCell(row, formatReading(record.readings?.flow));
    appendTextCell(row, formatReading(record.readings?.temperature));
    appendTextCell(row, record.batchId ?? "â€”");
    appendBadgeCell(row, record.status.toUpperCase(), `status-${record.status}`);

    const verification = verificationByRecord.get(record.id);
    appendVerificationCell(row, verification);

    elements.recordsBody.append(row);
  }
}

function appendTextCell(row, value) {
  const cell = document.createElement("td");
  cell.textContent = String(value);
  row.append(cell);
}

function appendBadgeCell(row, label, className) {
  const cell = document.createElement("td");
  const badge = document.createElement("span");
  badge.className = `status-badge ${className}`.trim();
  badge.textContent = label;
  cell.append(badge);
  row.append(cell);
}

function appendVerificationCell(row, verification) {
  const cell = document.createElement("td");
  const content = document.createElement("div");
  const badge = document.createElement("span");
  content.className = "verification-result";
  badge.className = `status-badge ${verification ? `status-${verification.status.toLowerCase()}` : ""}`.trim();
  badge.textContent = verification?.status || "NOT CHECKED";
  content.append(badge);

  if (verification?.verifiedAt) {
    const verifiedAt = document.createElement("span");
    verifiedAt.className = "verified-at";
    verifiedAt.textContent = `Verify at ${formatDateTime(verification.verifiedAt)}`;
    content.append(verifiedAt);
  }

  if (verification?.message) {
    const message = document.createElement("span");
    message.className = "verification-message";
    message.textContent = verification.message;
    content.append(message);
  }

  cell.append(content);
  row.append(cell);
}

async function verifyAllFromChain() {
  if (verifyAllInFlight) return;
  verifyAllInFlight = true;
  elements.verifyAllButton.disabled = true;
  elements.verifyAllButton.textContent = "Đang verify...";
  try {
    const errorsByBatch = await fetchJson("/api/batches/verify-all");

    const recordsByBatch = new Map();
    for (const record of records) {
      if (!record.batchId) continue;
      const batchRecords = recordsByBatch.get(record.batchId) || [];
      batchRecords.push(record.id);
      recordsByBatch.set(record.batchId, batchRecords);
    }

    const verifiedAt = new Date().toISOString();
    for (const [batchId, recordIds] of recordsByBatch) {
      const message = errorsByBatch[String(batchId)] || null;
      for (const recordId of recordIds) {
        verificationByRecord.set(recordId, {
          status: message ? "TAMPERED" : "VALID",
          verifiedAt,
          message: message || `Batch ${batchId}: valid.`
        });
      }
    }

    const tamperedMessages = Object.values(errorsByBatch);
    if (tamperedMessages.length === 0) {
      showToast("Tat ca batch dang hien thi deu hop le.");
    }
    else {
      showToast(tamperedMessages.join(" | "));
    }
  } catch (error) {
    showToast(error instanceof Error ? error.message : "Khong verify all duoc.");
  } finally {
    verifyAllInFlight = false;
    updateVerifyAllButton();
    renderTable();
  }
}

function getVerifiableRecords() {
  return records.filter(record => record.status === "anchored" && record.batchId);
}

function updateVerifyAllButton() {
  const verifiableRecords = getVerifiableRecords();
  elements.verifyAllButton.disabled = verifiableRecords.length === 0 || verifyAllInFlight;
  elements.verifyAllButton.textContent = verifyAllInFlight
    ? "Đang verify..."
    : `Verify all (${verifiableRecords.length})`;
}

function formatReading(value) {
  return value === null || value === undefined ? "â€”" : Number(value).toFixed(2);
}

function formatDateTime(value) {
  if (!value) return "â€”";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "â€”" : date.toLocaleString("vi-VN");
}

function setConnectionState(online) {
  elements.connectionStatus.classList.toggle("online", online);
  elements.connectionStatus.classList.toggle("offline", !online);
  elements.connectionStatus.lastElementChild.textContent = online ? "Äang hoáº¡t Ä‘á»™ng" : "Máº¥t káº¿t ná»‘i";
}

function showToast(message) {
  clearTimeout(toastTimer);
  elements.toast.textContent = message;
  elements.toast.hidden = false;
  toastTimer = setTimeout(() => {
    elements.toast.hidden = true;
  }, 5000);
}

elements.stationFilter.addEventListener("change", () => refreshDashboard());
elements.statusFilter.addEventListener("change", () => refreshDashboard());
elements.refreshButton.addEventListener("click", () => refreshDashboard({ refreshStations: true }));
elements.loadMoreButton.addEventListener("click", () => loadMoreRecords());
elements.verifyAllButton.addEventListener("click", () => verifyAllFromChain());
window.addEventListener("resize", renderCharts);

refreshDashboard({ refreshStations: true });
setInterval(() => refreshDashboard(), POLL_INTERVAL_MS);
