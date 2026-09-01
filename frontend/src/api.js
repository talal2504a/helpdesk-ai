import axios from 'axios';
import { HubConnectionBuilder, HttpTransportType } from '@microsoft/signalr';

export const TOKEN_KEY = 'helpdesk_token';
export const USER_KEY = 'helpdesk_user';

const isDev = import.meta.env.DEV;
const API_BASE = isDev ? 'http://localhost:5000/api' : '/api';
const HUB_URL = isDev ? 'http://localhost:5000/hubs/tickets' : '/hubs/tickets';

export const api = axios.create({ baseURL: API_BASE });

api.interceptors.request.use((cfg) => {
  const t = localStorage.getItem(TOKEN_KEY);
  if (t) cfg.headers.Authorization = `Bearer ${t}`;
  return cfg;
});

api.interceptors.response.use(
  (r) => r,
  (err) => {
    const d = err.response?.data;
    let msg = d?.error?.message || err.message || 'Request failed';
    const fieldErrors = d?.error?.errors || d?.errors;
    if (fieldErrors && typeof fieldErrors === 'object') {
      const parts = Object.values(fieldErrors).flat().filter(Boolean);
      if (parts.length) msg = parts.join(' ');
    }
    // Expired/invalid token: clear session and send user to login
    // (but don't hijack a failed login attempt - the login page shows that error itself)
    const url = err.config?.url || '';
    if (err.response?.status === 401 && !url.includes('/auth/login')) {
      clearSession();
      if (!window.location.pathname.startsWith('/login')) {
        window.location.replace('/login');
      }
    }
    return Promise.reject(new Error(msg));
  }
);

export function getUser() {
  try { return JSON.parse(localStorage.getItem(USER_KEY)); } catch { return null; }
}

export function saveSession(auth) {
  localStorage.setItem(TOKEN_KEY, auth.token);
  localStorage.setItem(USER_KEY, JSON.stringify({ id: auth.id, name: auth.name, email: auth.email, role: auth.role, expiresAtUtc: auth.expiresAtUtc }));
}

export function clearSession() {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
}

export async function toggleAiMode(ticketId, enable) {
  return api.post(`/tickets/${ticketId}/ai-mode`, { enable });
}

export async function takeOver(ticketId) {
  return api.post(`/tickets/${ticketId}/take-over`);
}

export async function deleteTicket(ticketId) {
  return api.delete(`/tickets/${ticketId}`);
}

export async function getAgents() {
  return api.get('/admin/agents');
}

export async function createAgent(data) {
  return api.post('/admin/agents', data);
}

export async function deleteAgent(agentId) {
  return api.delete(`/admin/agents/${agentId}`);
}

let connection = null;
let pollTimer = null;
let lastMessageId = 0;
let pollBusy = false;
const messageListeners = new Set();
const ticketListeners = new Set();
const notifListeners = new Set();
const aiStatusChangedListeners = new Set();
const humanAgentJoinedListeners = new Set();
const joinedGroups = new Set();

// ---------------------------------------------------------------------------
// HTTP Polling engine — SignalR replacement that works on ANY host.
// Polls /api/poll/messages every POLL_INTERVAL_MS with a sinceId cursor and
// fires the same listeners SignalR used to fire. Reliable everywhere.
// ---------------------------------------------------------------------------
const POLL_INTERVAL_MS = 2500;

async function pollTick() {
  if (pollBusy) return;
  pollBusy = true;
  try {
    const { data } = await api.get('/poll/messages', { params: { sinceId: lastMessageId } });
    if (Array.isArray(data) && data.length) {
      for (const m of data) {
        if (m.id > lastMessageId) lastMessageId = m.id;
        messageListeners.forEach((cb) => cb({
          id: m.id,
          ticketId: m.ticketId,
          body: m.body,
          createdAt: m.createdAt,
          isInternal: m.isInternal,
          isAiGenerated: m.isAiGenerated,
          senderId: m.senderId,
          senderName: m.senderName,
          senderRole: m.senderRole,
        }));
      }
    }
  } catch {
    // network hiccup or 401 — silently retry on next tick
  } finally {
    pollBusy = false;
  }
}

function startPolling() {
  stopPolling();
  pollTimer = setInterval(pollTick, POLL_INTERVAL_MS);
  // baseline: don't fire notifications for messages that existed before login
  api.get('/poll/latest')
    .then(({ data }) => { lastMessageId = data.latestId ?? 0; })
    .catch(() => {})
    .finally(() => pollTick());
}

function stopPolling() {
  if (pollTimer) { clearInterval(pollTimer); pollTimer = null; }
}

export async function startHub() {
  await stopHub();
  const token = localStorage.getItem(TOKEN_KEY);
  if (!token) return null;

  // Start HTTP polling immediately — this is the primary realtime channel.
  startPolling();

  // SignalR is best-effort only; if WebSockets are blocked by the host,
  // polling above keeps realtime working regardless.
  connection = new HubConnectionBuilder()
    .withUrl(HUB_URL, { 
      accessTokenFactory: () => token, 
      transport: HttpTransportType.Auto
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .build();

  connection.on('ReceiveMessage', (m) => {
    messageListeners.forEach((cb) => cb(m));
  });
  
  connection.on('TicketUpdated', (u) => {
    ticketListeners.forEach((cb) => cb(u));
  });
  
  connection.on('Notification', (n) => {
    notifListeners.forEach((cb) => cb(n));
  });
  
  connection.on('AiStatusChanged', (data) => {
    aiStatusChangedListeners.forEach((cb) => cb(data));
  });
  
  connection.on('HumanAgentJoined', (data) => {
    humanAgentJoinedListeners.forEach((cb) => cb(data));
  });

  connection.onreconnected(() => {
    joinedGroups.forEach(groupId => {
      connection?.invoke('JoinTicket', Number(groupId)).catch(() => {});
    });
  });

  try { 
    await connection.start(); 
  } catch (error) {
    console.error('SignalR connection failed:', error);
  }
  return connection;
}

export function onHubMessage(cb) { messageListeners.add(cb); return () => messageListeners.delete(cb); }
export function onHubTicket(cb) { ticketListeners.add(cb); return () => ticketListeners.delete(cb); }
export function onHubNotification(cb) { notifListeners.add(cb); return () => notifListeners.delete(cb); }
export function onAiStatusChanged(cb) { aiStatusChangedListeners.add(cb); return () => aiStatusChangedListeners.delete(cb); }
export function onHumanAgentJoined(cb) { humanAgentJoinedListeners.add(cb); return () => humanAgentJoinedListeners.delete(cb); }

export async function joinTicketGroup(ticketId) {
  // No-op with HTTP polling — visibility is enforced server-side per request.
  const groupId = String(ticketId);
  joinedGroups.add(groupId);
}

export async function leaveTicketGroup(ticketId) {
  const groupId = String(ticketId);
  joinedGroups.delete(groupId);
}

export async function stopHub() {
  stopPolling();
  lastMessageId = 0;
  joinedGroups.clear();
  messageListeners.clear(); 
  ticketListeners.clear(); 
  notifListeners.clear();
  aiStatusChangedListeners.clear(); 
  humanAgentJoinedListeners.clear();
  if (connection) { 
    try { await connection.stop(); } catch {} 
    connection = null; 
  }
}
