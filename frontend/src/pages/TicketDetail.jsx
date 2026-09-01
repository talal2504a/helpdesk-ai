import React, { useEffect, useState, useCallback, useRef } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useAuth } from '../AuthContext.jsx';
import { api, joinTicketGroup, onHubMessage, onHubTicket, onAiStatusChanged, onHumanAgentJoined, deleteTicket } from '../api.js';
import Loading, { BtnSpinner } from '../components/Spinner.jsx';

export default function TicketDetail() {
  const { id } = useParams();
  const { user } = useAuth();
  const staff = user.role !== 'Customer';
  const [ticket, setTicket] = useState(null);
  const [err, setErr] = useState('');
  const [body, setBody] = useState('');
  const [isInternal, setIsInternal] = useState(false);
  const [statuses, setStatuses] = useState([]);
  const [agents, setAgents] = useState([]);
  const [assignOpen, setAssignOpen] = useState(false);
  const [sending, setSending] = useState(false);
  const [analyzeBusy, setAnalyzeBusy] = useState(false);
  const [replyBusy, setReplyBusy] = useState(false);
  const [aiModeEnabled, setAiModeEnabled] = useState(true);
  const [waitingForAgent, setWaitingForAgent] = useState(false);
  const [aiStatusMessage, setAiStatusMessage] = useState('');
  const [humanJoinedMessage, setHumanJoinedMessage] = useState('');
  const [takingOver, setTakingOver] = useState(false);
  const [togglingAi, setTogglingAi] = useState(false);
  const messagesEndRef = useRef(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  const load = useCallback(async () => {
    try {
      const { data } = await api.get(`/tickets/${id}`);
      setTicket(data);
      setAiModeEnabled(data.ticket.aiModeEnabled ?? true);
      setWaitingForAgent(data.ticket.waitingForAgent ?? false);
      joinTicketGroup(id);
    } catch (e) { setErr(e.message); }
  }, [id]);

  useEffect(() => { api.get('/statuses').then(({ data }) => setStatuses(data)).catch(() => {}); }, []);
  useEffect(() => { if (staff) api.get('/users/agents').then(({ data }) => setAgents(data)).catch(() => {}); }, [staff]);
  useEffect(() => { load(); }, [load]);

  // HTTP polling fallback — auto-refresh ticket (messages + status) every 5s
  // so updates from other users appear without manual refresh.
  useEffect(() => {
    const iv = setInterval(() => { load(); }, 5000);
    return () => clearInterval(iv);
  }, [load]);

  useEffect(() => {
    const unMsg = onHubMessage((m) => {
      if (String(m.ticketId) === String(id)) {
        load();
        setTimeout(scrollToBottom, 100);
      }
    });
    const unTic = onHubTicket((u) => { if (u.ticketId && String(u.ticketId) === String(id)) load(); });
    return () => { unMsg(); unTic(); };
  }, [id, load]);

  useEffect(() => {
    const unAi = onAiStatusChanged((data) => {
      if (String(data.ticketId) === String(id)) {
        setAiModeEnabled(data.enabled);
        setWaitingForAgent(data.waitingForAgent || false);
        if (data.enabled) {
          setAiStatusMessage('AI is responding...');
          setTimeout(() => setAiStatusMessage(''), 3000);
        }
      }
    });
    const unHuman = onHumanAgentJoined((data) => {
      if (String(data.ticketId) === String(id)) {
        setHumanJoinedMessage(data.agentName ? `${data.agentName} has joined the conversation` : 'Human agent has joined the conversation');
        setTimeout(() => setHumanJoinedMessage(''), 5000);
      }
    });
    return () => { unAi(); unHuman(); };
  }, [id]);

  const send = async (e) => {
    e.preventDefault();
    if (!body.trim()) return;
    setSending(true);
    try {
      await api.post(`/tickets/${id}/messages`, { body, isInternal });
      setBody(''); setIsInternal(false);
      await load();
      setTimeout(scrollToBottom, 100);
    } catch (ex) { setErr(ex.message); } finally { setSending(false); }
  };

  const changeStatus = async (statusId) => { try { await api.post(`/tickets/${id}/status`, { statusId }); await load(); } catch (ex) { setErr(ex.message); } };
  const assign = async (agentId) => { try { await api.post(`/tickets/${id}/assign`, { agentId }); await load(); } catch (ex) { setErr(ex.message); } };
  const upload = async (f) => { const fd = new FormData(); [...f].forEach((x) => fd.append('files', x)); try { await api.post(`/tickets/${id}/attachments`, fd); await load(); } catch (ex) { setErr(ex.message); } };
  const runAnalyze = async () => { setAnalyzeBusy(true); try { await api.post(`/ai/analyze/${id}`); await load(); } catch (ex) { setErr(ex.message); } finally { setAnalyzeBusy(false); } };
  const runAiReply = async () => {
    setErr(''); setReplyBusy(true);
    try {
      const { data } = await api.post(`/ai/reply/${id}`);
      setBody(data.reply);
    } catch (ex) { setErr(ex.message); } finally { setReplyBusy(false); }
  };

  const handleTakeOver = async () => {
    setTakingOver(true);
    try {
      await api.post(`/tickets/${id}/take-over`);
      await load();
    } catch (ex) { setErr(ex.message); } finally { setTakingOver(false); }
  };

  const handleToggleAiMode = async () => {
    setTogglingAi(true);
    try {
      await api.post(`/tickets/${id}/ai-mode`, { enable: !aiModeEnabled });
      setAiModeEnabled(!aiModeEnabled);
      await load();
    } catch (ex) { setErr(ex.message); } finally { setTogglingAi(false); }
  };

  const isAiMessage = (m) => m.isAiGenerated || m.senderRole === 'AI' || m.senderName === 'AI Support';

  if (err && !ticket) return <div className="error">{err}</div>;
  if (!ticket) return <div className="h-[calc(100vh-64px)] flex items-center justify-center"><Loading size={36} label="Loading ticket…" /></div>;
  const t = ticket.ticket;

  return (
    <div className="flex flex-col md:h-[calc(100vh-64px)] w-full overflow-x-hidden">
      <div className="px-4 py-3 md:p-lg border-b border-outline-variant bg-surface-container-lowest flex-shrink-0">
        <div className="flex flex-col md:flex-row justify-between items-start gap-md">
          <div className="flex-1">
            <div className="flex flex-wrap items-center gap-sm mb-1">
              <span className="font-mono-sm text-mono-sm text-on-surface-variant">#{t.ticketNumber}</span>
              <span className={`px-2 py-0.5 rounded text-[11px] font-semibold tracking-wider uppercase ${
                t.status?.name === 'Open' ? 'bg-amber-100 text-amber-700' :
                t.status?.name === 'InProgress' ? 'bg-yellow-100 text-yellow-700' :
                t.status?.name === 'Resolved' ? 'bg-emerald-100 text-emerald-700' :
                t.status?.name === 'Closed' ? 'bg-gray-100 text-gray-700' :
                'bg-blue-100 text-blue-700'
              }`}>{t.status?.name}</span>
              <span className={`px-2 py-0.5 rounded text-[11px] font-semibold tracking-wider uppercase ${
                t.priority?.name === 'Critical' ? 'bg-red-100 text-red-700' :
                t.priority?.name === 'High' ? 'bg-orange-100 text-orange-700' :
                t.priority?.name === 'Medium' ? 'bg-yellow-100 text-yellow-700' :
                'bg-green-100 text-green-700'
              }`}>{t.priority?.name} Priority</span>
              <span className={`px-2 py-0.5 rounded text-[11px] font-semibold tracking-wider uppercase ${
                aiModeEnabled ? 'bg-emerald-100 text-emerald-700 anim-glow' : 'bg-red-100 text-red-700'
              }`}>
                <span className="material-symbols-outlined text-[12px] mr-1">smart_toy</span>
                {aiModeEnabled ? 'AI Active' : 'AI Off'}
              </span>
            </div>
            <h1 className="font-headline-md text-headline-md text-on-surface">{t.title}</h1>
          </div>
          <div className="flex flex-wrap gap-2 items-center">
            {staff && (
              <select
                value={t.status?.id || ''}
                onChange={(e) => changeStatus(e.target.value)}
                className="px-3 py-1.5 bg-surface-container-lowest border border-outline-variant text-on-surface rounded-lg font-label-md text-label-md cursor-pointer outline-none focus:border-primary"
                title="Change status"
              >
                {statuses.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
              </select>
            )}
            {staff && (
              <div className="relative">
                <button
                  onClick={() => setAssignOpen((o) => !o)}
                  className="px-3 py-1.5 bg-surface-container-lowest border border-outline-variant text-on-surface rounded-lg font-label-md text-label-md hover:bg-surface-container-low flex items-center gap-1 transition-colors"
                >
                  <span className="material-symbols-outlined text-[18px]">assignment_ind</span>
                  Assign
                </button>
                {assignOpen && (
                  <div className="absolute right-0 mt-2 w-56 bg-surface-container-lowest border border-outline-variant rounded-lg shadow-level-2 z-40 overflow-hidden">
                    <div className="px-md py-sm border-b border-outline-variant bg-surface-bright font-label-md text-label-md text-on-surface">Assign to agent</div>
                    <div className="max-h-56 overflow-y-auto msg-scroll">
                      {agents.map((a) => (
                        <button key={a.id} onClick={() => { setAssignOpen(false); assign(a.id); }}
                          className={`w-full text-left px-md py-sm font-body-sm text-body-sm hover:bg-surface-container-low transition-colors flex items-center gap-sm ${t.assignedAgentId === a.id ? 'text-primary font-semibold' : 'text-on-surface'}`}>
                          <span className="w-6 h-6 rounded-full bg-surface-variant flex items-center justify-center text-[10px] font-bold">
                            {a.name?.split(' ').map(n => n[0]).join('').toUpperCase()}
                          </span>
                          {a.name}
                          {t.assignedAgentId === a.id && <span className="material-symbols-outlined text-[16px] ml-auto">check</span>}
                        </button>
                      ))}
                      {agents.length === 0 && <div className="px-md py-sm text-on-surface-variant text-body-sm">No agents found.</div>}
                    </div>
                  </div>
                )}
              </div>
            )}
            {staff && (
              <button
                onClick={() => { const r = statuses.find((s) => s.name === 'Resolved'); if (r) changeStatus(r.id); }}
                className="px-3 py-1.5 bg-primary-container text-on-primary-container rounded-lg font-label-md text-label-md hover:opacity-90 transition-opacity flex items-center gap-1"
              >
                <span className="material-symbols-outlined text-[18px]">check_circle</span>
                Resolve
              </button>
            )}
            {user.role === 'Admin' && (
              <button
                onClick={async () => {
                  if (!confirm('Are you sure you want to delete this ticket? This action cannot be undone.')) return;
                  try {
                    await deleteTicket(id);
                    window.location.href = '/my-tickets';
                  } catch (ex) { setErr(ex.message); }
                }}
                className="px-3 py-1.5 bg-red-600 text-white rounded-lg font-label-md text-label-md hover:bg-red-700 transition-colors flex items-center gap-1"
                title="Delete ticket"
              >
                <span className="material-symbols-outlined text-[18px]">delete</span>
                Delete
              </button>
            )}
            {staff && (
              <button
                onClick={handleTakeOver}
                disabled={takingOver}
                className="px-3 py-1.5 bg-blue-600 text-white rounded-lg font-label-md text-label-md hover:bg-blue-700 transition-colors flex items-center gap-1 disabled:opacity-50"
              >
                {takingOver && <BtnSpinner />}
                {!takingOver && <span className="material-symbols-outlined text-[18px]">person</span>}
                {takingOver ? 'Taking Over...' : 'Take Over'}
              </button>
            )}
            {staff && (
              <button
                onClick={handleToggleAiMode}
                disabled={togglingAi}
                className={`px-3 py-1.5 rounded-lg font-label-md text-label-md transition-colors flex items-center gap-1 disabled:opacity-50 ${
                  aiModeEnabled ? 'bg-red-100 text-red-700 hover:bg-red-200' : 'bg-emerald-100 text-emerald-700 hover:bg-emerald-200'
                }`}
              >
                {togglingAi && <BtnSpinner />}
                {!togglingAi && <span className="material-symbols-outlined text-[18px]">smart_toy</span>}
                AI Mode: {aiModeEnabled ? 'ON' : 'OFF'}
              </button>
            )}
          </div>
        </div>
        <p className="font-body-md text-body-md text-on-surface-variant line-clamp-2 mt-sm">{t.description}</p>
      </div>

      {waitingForAgent && (
        <div className="bg-amber-50 border-b border-amber-200 px-lg py-3 flex items-center gap-2 animate-pulse">
          <span className="material-symbols-outlined text-amber-600 text-[20px]">schedule</span>
          <span className="font-label-md text-label-md text-amber-700">Waiting for human agent...</span>
        </div>
      )}

      {humanJoinedMessage && (
        <div className="bg-blue-50 border-b border-blue-200 px-lg py-3 flex items-center gap-2 anim-slide-down">
          <span className="material-symbols-outlined text-blue-600 text-[20px]">person</span>
          <span className="font-label-md text-label-md text-blue-700">{humanJoinedMessage}</span>
        </div>
      )}

      {aiStatusMessage && (
        <div className="bg-purple-50 border-b border-purple-200 px-lg py-3 flex items-center gap-2 anim-slide-down">
          <span className="material-symbols-outlined text-purple-600 text-[20px]">smart_toy</span>
          <span className="font-label-md text-label-md text-purple-700">{aiStatusMessage}</span>
        </div>
      )}

      <div className="flex-1 flex flex-col md:flex-row md:overflow-hidden">
        <section className="w-full md:w-[60%] flex flex-col border-r border-outline-variant bg-surface md:h-full min-h-0 min-w-0">
          <div className="h-[55vh] md:h-auto md:flex-1 md:min-h-0 overflow-y-auto p-4 md:p-lg bg-surface-container-low space-y-lg flex flex-col msg-scroll">
            {ticket.messages.map((m) => {
              const isAi = isAiMessage(m);
              const isOwn = m.senderId === user.id && !isAi;
              return (
                <div key={m.id} className={`flex items-start gap-md max-w-[85%] min-w-0 anim-slide-up ${isOwn ? 'self-end flex-row-reverse' : ''}`}>
                  <div className={`w-8 h-8 rounded-full flex items-center justify-center font-bold text-sm flex-shrink-0 ${
                    isAi ? 'bg-purple-100 text-purple-700 anim-floaty' : 'bg-surface-variant'
                  }`}>
                    {isAi ? <span className="material-symbols-outlined text-[16px]">smart_toy</span> : m.senderName?.split(' ').map(n => n[0]).join('').toUpperCase() || 'U'}
                  </div>
                  <div className="flex flex-col min-w-0">
                    <div className={`flex flex-wrap items-center gap-2 mb-1 min-w-0 ${isOwn ? 'flex-row-reverse' : ''}`}>
                      <span className={`font-label-md text-label-md ${isAi ? 'text-purple-700' : 'text-on-surface'}`}>
                        {isAi ? 'AI Support' : m.senderName}
                      </span>
                      <span className="font-body-sm text-body-sm text-on-surface-variant whitespace-nowrap">{new Date(m.createdAt).toLocaleString()}</span>
                      {m.isInternal && <span className="px-2 py-0.5 rounded text-[10px] font-semibold bg-amber-100 text-amber-700 uppercase tracking-wider">Internal</span>}
                      {isAi && <span className="px-2 py-0.5 rounded text-[10px] font-semibold bg-purple-100 text-purple-700 uppercase tracking-wider">AI</span>}
                    </div>
                    <div className={`p-4 rounded-xl shadow-sm min-w-0 max-w-full ${
                      isAi ? 'bg-gradient-to-br from-purple-50 to-indigo-50 border border-purple-200 rounded-tl-sm' :
                      isOwn ? 'bg-primary-container text-on-primary-container rounded-tr-sm' :
                      'bg-surface-container-lowest border border-outline-variant rounded-tl-sm'
                    }`}>
                      <p className="font-body-md text-body-md break-words whitespace-pre-wrap">{m.body}</p>
                      {m.attachments?.length > 0 && (
                        <div className="mt-3 flex flex-wrap gap-2">
                          {m.attachments.map((a) => (
                            <a key={a.id} href={a.fileUrl} target="_blank" rel="noreferrer" className="flex items-center gap-1 px-2 py-1 bg-surface-container rounded border border-outline-variant text-xs text-on-surface-variant hover:bg-surface-variant max-w-full min-w-0">
                              <span className="material-symbols-outlined text-[16px]">image</span>
                              <span className="truncate max-w-[180px] sm:max-w-xs">{a.fileName}</span>
                            </a>
                          ))}
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              );
            })}
            <div ref={messagesEndRef} />
            {ticket.messages.length === 0 && <p className="muted text-center py-lg">No messages yet.</p>}
          </div>

          <div className="p-4 md:p-lg bg-surface-container-lowest border-t border-outline-variant flex-shrink-0">
            <form onSubmit={send} className="flex flex-col gap-sm">
              <div className="border border-outline-variant rounded-lg bg-surface focus-within:border-primary focus-within:ring-2 focus-within:ring-primary/10 transition-all flex flex-col">
                <textarea
                  className="w-full bg-transparent border-none resize-none p-3 font-body-md text-body-md focus:ring-0 text-on-surface max-h-40"
                  placeholder="Type your reply... (Enter to send, Shift+Enter for new line)"
                  rows="3"
                  value={body}
                  onChange={(e) => setBody(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' && !e.shiftKey) {
                      e.preventDefault();
                      if (body.trim() && !sending) send(e);
                    }
                  }}
                />
                <div className="flex flex-wrap justify-between items-center gap-2 p-2 bg-surface-container-lowest border-t border-outline-variant/50 rounded-b-lg">
                  <div className="flex gap-1">
                    <label className="p-1.5 text-on-surface-variant hover:bg-surface-container-low rounded transition-colors cursor-pointer" title="Attach file">
                      <span className="material-symbols-outlined text-[20px]">attach_file</span>
                      <input type="file" multiple hidden onChange={(e) => upload(e.target.files)} />
                    </label>
                    {staff && (
                      <label className="p-1.5 text-amber-500 hover:bg-amber-100/50 rounded transition-colors flex items-center gap-1 font-label-md text-label-md cursor-pointer" title="Add internal note">
                        <span className="material-symbols-outlined text-[18px]">lock</span>
                        <input type="checkbox" className="hidden" checked={isInternal} onChange={(e) => setIsInternal(e.target.checked)} />
                        Note
                      </label>
                    )}
                  </div>
                  <button type="submit" className="flex-1 sm:flex-none px-4 py-2 bg-primary-container text-on-primary-container rounded-md font-label-md text-label-md hover:opacity-90 transition-opacity flex justify-center items-center gap-2 disabled:opacity-50" disabled={!body.trim() || sending}>
                    {sending && <BtnSpinner />}
                    {sending ? 'Sending…' : 'Send Reply'}
                    {!sending && <span className="material-symbols-outlined text-[16px]">send</span>}
                  </button>
                </div>
              </div>
            </form>
          </div>
        </section>

        {staff && (
          <section className="w-full md:w-[40%] bg-surface-container-lowest md:overflow-y-auto p-4 md:p-lg space-y-lg msg-scroll border-t md:border-t-0 border-outline-variant">
            {ticket.latestAi && (
              <div className="bg-surface border border-outline-variant rounded-xl p-md shadow-sm">
                <h2 className="font-headline-md text-headline-md text-on-surface mb-3 flex items-center gap-2">
                  <span className="material-symbols-outlined text-primary">auto_awesome</span>
                  Copilot Insights
                </h2>
                <div className="grid grid-cols-2 gap-3 mb-3">
                  <div className="bg-surface-container-lowest p-3 rounded-lg border border-outline-variant/50">
                    <div className="text-on-surface-variant text-[11px] uppercase tracking-wider font-semibold mb-1">Sentiment</div>
                    <div className="text-on-surface font-semibold text-body-md">{ticket.latestAi.sentiment}</div>
                  </div>
                  <div className="bg-surface-container-lowest p-3 rounded-lg border border-outline-variant/50">
                    <div className="text-on-surface-variant text-[11px] uppercase tracking-wider font-semibold mb-1">Category</div>
                    <div className="text-on-surface font-semibold text-body-md">{ticket.latestAi.category}</div>
                  </div>
                </div>
                <div className="bg-surface-container-lowest p-3 rounded-lg border border-outline-variant/50 mb-3">
                  <div className="text-on-surface-variant text-[11px] uppercase tracking-wider font-semibold mb-1">Summary</div>
                  <p className="text-on-surface text-body-sm">{ticket.latestAi.summary}</p>
                </div>
                {ticket.latestAi.suggestedReply && (
                  <div className="bg-surface-container-lowest p-3 rounded-lg border border-outline-variant/50">
                    <div className="text-on-surface-variant text-[11px] uppercase tracking-wider font-semibold mb-1">Suggested Reply</div>
                    <p className="text-on-surface text-body-sm whitespace-pre-wrap">{ticket.latestAi.suggestedReply}</p>
                    <button className="mt-2 text-sm text-primary hover:underline" onClick={() => setBody(ticket.latestAi.suggestedReply)}>Use this reply</button>
                  </div>
                )}
              </div>
            )}

            {staff && (
              <div className="bg-surface border border-outline-variant rounded-xl p-md shadow-sm">
                <h2 className="font-headline-md text-headline-md text-on-surface mb-3">Actions</h2>
                <div className="flex flex-col gap-2">
                  <button onClick={runAnalyze} disabled={analyzeBusy} className="w-full py-2 bg-surface border border-outline-variant text-on-surface rounded-lg font-label-md text-label-md hover:bg-surface-container-low flex justify-center items-center gap-2 transition-colors disabled:opacity-50">
                    {analyzeBusy ? <BtnSpinner /> : <span className="material-symbols-outlined text-[18px]">auto_awesome</span>}
                    {analyzeBusy ? 'Analyzing…' : 'Re-analyze AI'}
                  </button>
                  <button onClick={runAiReply} disabled={replyBusy} className="w-full py-2 bg-primary-container text-on-primary-container rounded-lg font-label-md text-label-md hover:opacity-90 transition-opacity flex justify-center items-center gap-2 disabled:opacity-50">
                    {replyBusy ? <BtnSpinner /> : <span className="material-symbols-outlined text-[18px]">chat</span>}
                    {replyBusy ? 'Generating…' : 'AI Reply'}
                  </button>
                </div>
              </div>
            )}

            {ticket.attachments?.length > 0 && (
              <div className="bg-surface border border-outline-variant rounded-xl p-md shadow-sm">
                <h2 className="font-headline-md text-headline-md text-on-surface mb-3 flex items-center gap-2">
                  <span className="material-symbols-outlined text-outline">folder</span>
                  Attachments ({ticket.attachments.length})
                </h2>
                <div className="space-y-2">
                  {ticket.attachments.map((a) => (
                    <a key={a.id} href={a.fileUrl} target="_blank" rel="noreferrer" className="flex items-center justify-between p-2 border border-outline-variant rounded-lg hover:bg-surface-container-low transition-colors">
                      <div className="flex items-center gap-3">
                        <div className="w-8 h-8 bg-surface-container-high rounded flex items-center justify-center text-on-surface-variant">
                          <span className="material-symbols-outlined text-[20px]">image</span>
                        </div>
                        <div>
                          <div className="font-body-sm text-on-surface font-medium">{a.fileName}</div>
                          <div className="text-[11px] text-on-surface-variant">{(a.fileSize / 1024).toFixed(1)} KB</div>
                        </div>
                      </div>
                      <span className="material-symbols-outlined text-[20px] text-on-surface-variant">download</span>
                    </a>
                  ))}
                </div>
              </div>
            )}

            {ticket.history?.length > 0 && (
              <div className="bg-surface border border-outline-variant rounded-xl p-md shadow-sm">
                <h2 className="font-headline-md text-headline-md text-on-surface mb-3">Ticket History</h2>
                <div className="space-y-3">
                  {ticket.history.map((h) => (
                    <div key={h.id} className="flex gap-3">
                      <div className="w-2 h-2 rounded-full bg-primary mt-2 flex-shrink-0"></div>
                      <div>
                        <div className="text-sm text-on-surface"><span className="font-semibold">{h.action}</span> {h.oldValue && <span className="text-on-surface-variant">from {h.oldValue}</span>} {h.newValue && <span className="text-on-surface-variant">to {h.newValue}</span>}</div>
                        <div className="text-xs text-on-surface-variant">{h.userName} · {new Date(h.createdAt).toLocaleString()}</div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </section>
        )}
      </div>
      {err && <div className="error">{err}</div>}
    </div>
  );
}
