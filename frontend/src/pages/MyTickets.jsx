import React, { useEffect, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../AuthContext.jsx';
import { api } from '../api.js';
import Loading from '../components/Spinner.jsx';

export default function MyTickets() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const staff = user.role !== 'Customer';
  const [searchParams] = useSearchParams();
  const [data, setData] = useState(null);
  const [err, setErr] = useState('');
  const [filters, setFilters] = useState({ statusId: '', priorityId: '', categoryId: '', search: searchParams.get('search') || '', page: 1 });
  const [sortDir, setSortDir] = useState('desc');
  const [statuses, setStatuses] = useState([]);
  const [priorities, setPriorities] = useState([]);
  const [categories, setCategories] = useState([]);

  useEffect(() => { api.get('/statuses').then(({ data }) => setStatuses(data)).catch(() => {}); }, []);
  useEffect(() => { api.get('/priorities').then(({ data }) => setPriorities(data)).catch(() => {}); }, []);
  useEffect(() => { api.get('/categories').then(({ data }) => setCategories(data)).catch(() => {}); }, []);

  useEffect(() => {
    const s = searchParams.get('search') || '';
    if (s !== filters.search) setFilters((f) => ({ ...f, search: s, page: 1 }));
  }, [searchParams]);

  useEffect(() => {
    setErr('');
    const params = new URLSearchParams();
    Object.entries(filters).forEach(([k, v]) => { if (v) params.set(k, v); });
    params.set('sortBy', 'CreatedAt');
    params.set('sortDir', sortDir);
    api.get(`/tickets?${params.toString()}`).then(({ data }) => setData(data)).catch((e) => setErr(e.message));
  }, [filters, sortDir, staff]);

  const getAiBadge = (t) => {
    if (t.waitingForAgent) {
      return <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-semibold bg-amber-100 text-amber-700 uppercase tracking-wider"><span className="material-symbols-outlined text-[10px]">schedule</span>Waiting</span>;
    }
    if (!t.aiModeEnabled && t.assignedAgentId) {
      return <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-semibold bg-blue-100 text-blue-700 uppercase tracking-wider"><span className="material-symbols-outlined text-[10px]">person</span>Human</span>;
    }
    if (t.aiModeEnabled) {
      return <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-semibold bg-emerald-100 text-emerald-700 uppercase tracking-wider"><span className="material-symbols-outlined text-[10px]">smart_toy</span>AI</span>;
    }
    return null;
  };

  return (
    <div className="max-w-[1600px] w-full mx-auto px-4">
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-md my-lg">
        <h1 className="font-headline-lg text-headline-lg text-on-surface">{staff ? 'All Tickets' : 'My Tickets'}</h1>
        <div className="flex flex-wrap items-center gap-sm">
          <div className="flex items-center gap-sm bg-surface-container-lowest border border-outline-variant rounded-lg p-1 shadow-sm">
            <select className="border-none bg-transparent text-body-sm font-body-sm text-on-surface focus:ring-0 cursor-pointer pr-8 py-1.5"
              value={filters.statusId} onChange={(e) => setFilters({ ...filters, statusId: e.target.value, page: 1 })}>
              <option value="">Status: All</option>
              {statuses.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
            </select>
            <div className="w-px h-4 bg-outline-variant"></div>
            <select className="border-none bg-transparent text-body-sm font-body-sm text-on-surface focus:ring-0 cursor-pointer pr-8 py-1.5"
              value={filters.priorityId} onChange={(e) => setFilters({ ...filters, priorityId: e.target.value, page: 1 })}>
              <option value="">Priority: All</option>
              {priorities.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
            </select>
            <div className="w-px h-4 bg-outline-variant"></div>
            <select className="border-none bg-transparent text-body-sm font-body-sm text-on-surface focus:ring-0 cursor-pointer pr-8 py-1.5"
              value={filters.categoryId} onChange={(e) => setFilters({ ...filters, categoryId: e.target.value, page: 1 })}>
              <option value="">Category: All</option>
              {categories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </select>
          </div>

          <button
            onClick={() => setSortDir((d) => (d === 'desc' ? 'asc' : 'desc'))}
            className="flex items-center gap-1 bg-surface-container-lowest border border-outline-variant rounded-lg px-3 py-1.5 shadow-sm cursor-pointer hover:bg-surface-container-low transition-colors"
            title="Toggle sort order"
          >
            <span className="material-symbols-outlined text-[18px] text-on-surface-variant">sort</span>
            <span className="font-body-sm text-body-sm text-on-surface">{sortDir === 'desc' ? 'Newest' : 'Oldest'}</span>
          </button>

          {!staff && (
            <Link to="/tickets/new" className="flex items-center gap-1 bg-primary text-on-primary font-label-md text-label-md px-4 py-2 rounded-lg shadow-sm hover:opacity-90 active:scale-[0.98] transition-all ml-2">
              <span className="material-symbols-outlined text-[18px]">add</span>
              Create Ticket
            </Link>
          )}
        </div>
      </div>

      <div className="bg-surface-container-lowest border border-outline-variant rounded-lg overflow-hidden">
        {err && <div className="error p-md border-b border-outline-variant">{err}</div>}
        {!data && <Loading label="Loading tickets…" />}

        {data && (
          <>
            <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse">
                <thead>
                  <tr className="bg-surface-container-low border-b border-outline-variant font-label-md text-label-md text-on-surface-variant uppercase tracking-wider">
                    <th className="px-md py-3 font-semibold">Ticket #</th>
                    <th className="px-md py-3 font-semibold">Title</th>
                    <th className="px-md py-3 font-semibold">Customer</th>
                    <th className="px-md py-3 font-semibold">Status</th>
                    <th className="px-md py-3 font-semibold">Priority</th>
                    <th className="px-md py-3 font-semibold">Mode</th>
                    <th className="px-md py-3 font-semibold">Assigned To</th>
                    <th className="px-md py-3 font-semibold text-right">Created</th>
                  </tr>
                </thead>
                <tbody className="font-body-md text-body-md text-on-surface divide-y divide-outline-variant">
                  {data.items.map((t) => (
                    <tr key={t.id} className="hover:bg-surface-container-lowest/50 group cursor-pointer transition-colors"
                    onClick={() => navigate(`/tickets/${t.id}`)}
                    title="Open ticket">
                      <td className="px-md py-4 font-mono-sm text-mono-sm text-primary">{t.ticketNumber}</td>
                      <td className="px-md py-4 font-medium text-on-surface w-1/4">
                        <Link to={`/tickets/${t.id}`} className="hover:text-primary transition-colors">{t.title}</Link>
                      </td>
                      <td className="px-md py-4">
                        <div className="flex items-center gap-2">
                          <div className="w-6 h-6 rounded-full bg-surface-variant flex items-center justify-center font-bold text-xs">
                            {t.customerName?.split(' ').map(n => n[0]).join('').toUpperCase() || 'U'}
                          </div>
                          {t.customerName}
                        </div>
                      </td>
                      <td className="px-md py-4">
                        <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold ${
                          t.status?.name === 'Open' ? 'bg-amber-100 text-amber-700' :
                          t.status?.name === 'InProgress' ? 'bg-yellow-100 text-yellow-700' :
                          t.status?.name === 'Resolved' ? 'bg-emerald-100 text-emerald-700' :
                          t.status?.name === 'Closed' ? 'bg-gray-100 text-gray-700' :
                          'bg-blue-100 text-blue-700'
                        }`}>{t.status?.name}</span>
                      </td>
                      <td className="px-md py-4">
                        <span className={`inline-flex items-center gap-1 text-sm ${
                          t.priority?.name === 'Critical' ? 'text-red-600' :
                          t.priority?.name === 'High' ? 'text-orange-600' :
                          t.priority?.name === 'Medium' ? 'text-yellow-600' :
                          'text-green-600'
                        }`}>
                          {t.priority?.name === 'High' || t.priority?.name === 'Critical' ? (
                            <span className="material-symbols-outlined text-[16px] fill">keyboard_double_arrow_up</span>
                          ) : t.priority?.name === 'Low' ? (
                            <span className="material-symbols-outlined text-[16px]">keyboard_arrow_down</span>
                          ) : (
                            <span className="material-symbols-outlined text-[16px]">keyboard_arrow_up</span>
                          )}
                          {t.priority?.name}
                        </span>
                      </td>
                      <td className="px-md py-4">{getAiBadge(t)}</td>
                      <td className="px-md py-4 text-on-surface-variant">{t.assignedAgentName || 'Unassigned'}</td>
                      <td className="px-md py-4 text-right text-on-surface-variant text-sm">{new Date(t.createdAt).toLocaleDateString()}</td>
                    </tr>
                  ))}
                  {data.items.length === 0 && (
                    <tr><td colSpan="8" className="px-md py-lg text-center text-on-surface-variant">No tickets found.</td></tr>
                  )}
                </tbody>
              </table>
            </div>

            {data.totalPages > 1 && (
              <div className="bg-surface-container-low border-t border-outline-variant px-md py-3 flex items-center justify-between">
                <div className="text-sm text-on-surface-variant">
                  Showing <span className="font-medium text-on-surface">1</span> to <span className="font-medium text-on-surface">{data.items.length}</span> of <span className="font-medium text-on-surface">{data.totalCount}</span> results
                </div>
                <div className="flex items-center gap-2">
                  <button onClick={() => setFilters({ ...filters, page: data.page - 1 })} className="p-1 rounded text-on-surface-variant hover:bg-surface-variant disabled:opacity-50 transition-colors" disabled={data.page <= 1}>
                    <span className="material-symbols-outlined text-[20px]">chevron_left</span>
                  </button>
                  <div className="flex items-center gap-1">
                    {[...Array(Math.min(5, data.totalPages))].map((_, i) => {
                      const pageNum = data.page <= 3 ? i + 1 : Math.min(data.totalPages, data.page + 2) - (4 - i);
                      return (
                        <button key={pageNum} onClick={() => setFilters({ ...filters, page: pageNum })} className={`w-8 h-8 rounded font-medium text-sm flex items-center justify-center ${data.page === pageNum ? 'bg-primary text-on-primary' : 'text-on-surface-variant hover:bg-surface-variant'}`}>
                          {pageNum}
                        </button>
                      );
                    })}
                  </div>
                  <button onClick={() => setFilters({ ...filters, page: data.page + 1 })} className="p-1 rounded text-on-surface-variant hover:bg-surface-variant transition-colors" disabled={data.page >= data.totalPages}>
                    <span className="material-symbols-outlined text-[20px]">chevron_right</span>
                  </button>
                </div>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
