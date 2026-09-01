import React, { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../api.js';
import { useAuth } from '../AuthContext.jsx';
import Loading, { BtnSpinner } from '../components/Spinner.jsx';

export default function AgentQueue() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const [view, setView] = useState('queue');
  const [data, setData] = useState(null);
  const [err, setErr] = useState('');
  const [claiming, setClaiming] = useState(null);
  const [takingOver, setTakingOver] = useState(null);

  const claim = async (t) => {
    setErr(''); setClaiming(t.id);
    try {
      await api.post(`/tickets/${t.id}/assign`, { agentId: user.id });
      setView('queue');
    } catch (ex) { setErr(ex.message); } finally { setClaiming(null); }
  };

  const handleTakeOver = async (t) => {
    setErr(''); setTakingOver(t.id);
    try {
      await api.post(`/tickets/${t.id}/take-over`);
    } catch (ex) { setErr(ex.message); } finally { setTakingOver(null); }
  };

  useEffect(() => {
    setErr('');
    const url = view === 'unassigned' ? '/tickets/unassigned' : view === 'all' ? '/tickets?pageSize=50' : '/tickets/my-queue';
    api.get(url).then(({ data }) => setData(data)).catch((e) => setErr(e.message));
  }, [view]);

  const getAiStatusBadge = (t) => {
    if (t.waitingForAgent) {
      return <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-semibold bg-amber-100 text-amber-700 uppercase tracking-wider"><span className="material-symbols-outlined text-[10px]">schedule</span>Waiting</span>;
    }
    if (!t.aiModeEnabled && t.assignedAgentId) {
      return <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-semibold bg-blue-100 text-blue-700 uppercase tracking-wider"><span className="material-symbols-outlined text-[10px]">person</span>Human</span>;
    }
    if (t.aiModeEnabled) {
      return <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-semibold bg-emerald-100 text-emerald-700 uppercase tracking-wider"><span className="material-symbols-outlined text-[10px]">smart_toy</span>AI</span>;
    }
    return <span className="text-on-surface-variant text-xs">—</span>;
  };

  return (
    <div className="max-w-[1600px] w-full mx-auto px-4">
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-md my-lg">
        <h1 className="font-headline-lg text-headline-lg text-on-surface">Agent Queue</h1>
        <div className="flex gap-sm">
          <button className={`font-label-md text-label-md px-md py-sm rounded-lg transition-colors ${view === 'queue' ? 'bg-primary-container text-on-primary-container' : 'text-on-surface-variant hover:bg-surface-container-low'}`} onClick={() => setView('queue')}>My Queue</button>
          <button className={`font-label-md text-label-md px-md py-sm rounded-lg transition-colors ${view === 'unassigned' ? 'bg-primary-container text-on-primary-container' : 'text-on-surface-variant hover:bg-surface-container-low'}`} onClick={() => setView('unassigned')}>Unassigned</button>
          <button className={`font-label-md text-label-md px-md py-sm rounded-lg transition-colors ${view === 'all' ? 'bg-primary-container text-on-primary-container' : 'text-on-surface-variant hover:bg-surface-container-low'}`} onClick={() => setView('all')}>All Tickets</button>
        </div>
      </div>

      {err && <div className="error mb-lg">{err}</div>}

      <div className="bg-surface-container-lowest border border-outline-variant rounded-lg overflow-hidden">
        {!data && <Loading label="Loading queue…" />}
        {data && (
          <>
            <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse">
                <thead>
                  <tr className="bg-surface-container-low border-b border-outline-variant font-label-md text-label-md text-on-surface-variant uppercase tracking-wider">
                    <th className="px-md py-3 font-semibold">Ticket #</th>
                    <th className="px-md py-3 font-semibold">Title</th>
                    <th className="px-md py-3 font-semibold">Customer</th>
                    <th className="px-md py-3 font-semibold">Priority</th>
                    <th className="px-md py-3 font-semibold">Status</th>
                    <th className="px-md py-3 font-semibold">AI Mode</th>
                    <th className="px-md py-3 font-semibold">Assigned</th>
                    <th className="px-md py-3 font-semibold text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="font-body-md text-body-md divide-y divide-outline-variant">
                  {data.items.map((t) => (
                    <tr key={t.id} className="hover:bg-surface-container-low/50 transition-colors cursor-pointer" onClick={() => navigate(`/tickets/${t.id}`)} title="Open ticket">
                      <td className="px-md py-4 font-mono-sm text-mono-sm text-primary">
                        <Link to={`/tickets/${t.id}`} className="hover:underline">{t.ticketNumber}</Link>
                      </td>
                      <td className="px-md py-4 font-medium text-on-surface">
                        <Link to={`/tickets/${t.id}`} className="hover:text-primary transition-colors">{t.title}</Link>
                      </td>
                      <td className="px-md py-4 text-on-surface-variant">{t.customerName}</td>
                      <td className="px-md py-4">
                        <span className={`inline-flex items-center gap-1 text-sm ${
                          t.priority?.name === 'Critical' ? 'text-red-600' :
                          t.priority?.name === 'High' ? 'text-orange-600' :
                          t.priority?.name === 'Medium' ? 'text-yellow-600' :
                          'text-green-600'
                        }`}>
                          {t.priority?.name}
                        </span>
                      </td>
                      <td className="px-md py-4">
                        <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold ${
                          t.status?.name === 'Open' ? 'bg-amber-100 text-amber-700' :
                          t.status?.name === 'InProgress' ? 'bg-yellow-100 text-yellow-700' :
                          t.status?.name === 'Resolved' ? 'bg-emerald-100 text-emerald-700' :
                          'bg-gray-100 text-gray-700'
                        }`}>{t.status?.name}</span>
                      </td>
                      <td className="px-md py-4">{getAiStatusBadge(t)}</td>
                      <td className="px-md py-4 text-on-surface-variant">{t.assignedAgentName || '—'}</td>
                      <td className="px-md py-4 text-right">
                        <div className="flex items-center justify-end gap-2">
                          {!t.assignedAgentId && (
                            <button
                              onClick={(e) => { e.stopPropagation(); claim(t); }}
                              disabled={claiming === t.id}
                              className="px-3 py-1 bg-primary text-on-primary rounded-lg font-label-md text-label-md hover:opacity-90 transition-opacity disabled:opacity-50 inline-flex items-center gap-1"
                              title="Assign this ticket to me"
                            >
                              {claiming === t.id ? <BtnSpinner /> : <span className="material-symbols-outlined text-[14px]">back_hand</span>}
                              {claiming === t.id ? 'Claiming…' : 'Claim'}
                            </button>
                          )}
                          <button
                            onClick={(e) => { e.stopPropagation(); handleTakeOver(t); }}
                            disabled={takingOver === t.id}
                            className="px-3 py-1 bg-blue-600 text-white rounded-lg font-label-md text-label-md hover:bg-blue-700 transition-colors disabled:opacity-50 inline-flex items-center gap-1"
                            title="Take over this ticket"
                          >
                            {takingOver === t.id ? <BtnSpinner /> : <span className="material-symbols-outlined text-[14px]">person</span>}
                            {takingOver === t.id ? 'Taking Over...' : 'Take Over'}
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                  {data.items.length === 0 && <tr><td colSpan="8" className="px-md py-lg text-center text-on-surface-variant">Nothing here.</td></tr>}
                </tbody>
              </table>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
