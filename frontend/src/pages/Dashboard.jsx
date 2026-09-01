import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api.js';
import Loading from '../components/Spinner.jsx';
import { useNavigate } from 'react-router-dom';

const statusChip = (name) =>
  name === 'Open' ? 'bg-[#f59e0b]/10 text-[#f59e0b]' :
  name === 'InProgress' ? 'bg-[#eab308]/10 text-[#eab308]' :
  name === 'Resolved' ? 'bg-[#10b981]/10 text-[#10b981]' :
  name === 'Closed' ? 'bg-[#64748b]/10 text-[#64748b]' :
  'bg-[#3b82f6]/10 text-[#3b82f6]';

const prioChip = (name) =>
  name === 'Critical' || name === 'High' ? 'bg-[#ef4444]/10 text-[#ef4444]' :
  name === 'Medium' ? 'bg-[#3b82f6]/10 text-[#3b82f6]' :
  'bg-[#64748b]/10 text-[#64748b]';

export default function Dashboard() {
  const navigate = useNavigate();
  const [s, setS] = useState(null);
  const [recent, setRecent] = useState([]);
  const [err, setErr] = useState('');
  useEffect(() => { api.get('/dashboard/stats').then(({ data }) => setS(data)).catch((e) => setErr(e.message)); }, []);
  useEffect(() => { api.get('/tickets?pageSize=5&sortBy=CreatedAt&sortDir=desc').then(({ data }) => setRecent(data.items || [])).catch(() => {}); }, []);

  if (err) return <div className="error m-lg">{err}</div>;
  if (!s) return <div className="pt-16"><Loading size={36} label="Loading dashboardâ€¦" /></div>;

  const cards = [
    ['Total Tickets', s.totalTickets, 'confirmation_number', '#3525cd'],
    ['Open Tickets', s.openTickets, 'error_outline', '#f59e0b'],
    ['In Progress', s.inProgressTickets, 'pending_actions', '#eab308'],
    ['Resolved', s.resolvedTickets, 'task_alt', '#10b981'],
    ['Closed', s.closedTickets, 'archive', '#64748b'],
  ];

  const aiCards = [
    ['AI Active', s.aiActiveTickets || 0, 'smart_toy', '#8b5cf6'],
    ['Waiting for Agent', s.waitingForAgentTickets || 0, 'schedule', '#f59e0b'],
    ['Human Agent', s.humanAgentTickets || 0, 'person', '#3b82f6'],
    ['AI Resolution Rate', `${s.aiResolutionRate || 0}%`, 'auto_awesome', '#10b981'],
  ];

  return (
    <div className="max-w-7xl mx-auto px-4">
      <header className="mb-lg mt-md flex justify-between items-end">
        <div>
          <h1 className="font-headline-lg text-headline-lg text-on-background">Overview Dashboard</h1>
          <p className="font-body-sm text-body-sm text-on-surface-variant mt-unit">Real-time metrics and active issues.</p>
        </div>
        <Link to="/tickets" className="bg-primary text-on-primary font-label-md text-label-md px-md py-sm rounded-lg hover:opacity-90 transition-opacity flex items-center gap-sm">
          <span className="material-symbols-outlined text-[18px]">list_alt</span>
          All Tickets
        </Link>
      </header>

      <section className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-5 gap-md mb-xl">
        {cards.map(([label, value, icon, color]) => (
          <div key={label} className="bg-surface-container-lowest border border-outline-variant rounded-lg p-md flex flex-col hover:bg-surface-container-low transition-colors hover-lift anim-slide-up">
            <div className="flex justify-between items-start mb-sm">
              <span className="font-label-md text-label-md text-on-surface-variant">{label}</span>
              <div className="p-unit rounded" style={{ backgroundColor: `${color}1a`, color }}>
                <span className="material-symbols-outlined text-[20px]">{icon}</span>
              </div>
            </div>
            <div className="font-display-lg text-display-lg text-on-surface">{value}</div>
          </div>
        ))}
      </section>

      <section className="mb-xl">
        <h2 className="font-headline-md text-headline-md text-on-surface mb-md flex items-center gap-2">
          <span className="material-symbols-outlined text-primary">smart_toy</span>
          AI Metrics
        </h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-md">
          {aiCards.map(([label, value, icon, color]) => (
            <div key={label} className="bg-surface-container-lowest border border-outline-variant rounded-lg p-md flex flex-col hover:bg-surface-container-low transition-colors hover-lift anim-slide-up">
              <div className="flex justify-between items-start mb-sm">
                <span className="font-label-md text-label-md text-on-surface-variant">{label}</span>
                <div className="p-unit rounded" style={{ backgroundColor: `${color}1a`, color }}>
                  <span className="material-symbols-outlined text-[20px]">{icon}</span>
                </div>
              </div>
              <div className="font-display-lg text-display-lg text-on-surface">{value}</div>
            </div>
          ))}
        </div>
      </section>

      <section className="bg-surface-container-lowest border border-outline-variant rounded-lg overflow-hidden">
        <div className="px-md py-md border-b border-outline-variant flex justify-between items-center bg-surface-bright">
          <h2 className="font-headline-md text-headline-md text-on-surface">Recent Tickets</h2>
          <Link to="/tickets" className="font-label-md text-label-md text-primary hover:underline">View All</Link>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="border-b border-outline-variant bg-surface-container-low">
                <th className="px-md py-sm font-label-md text-label-md text-on-surface-variant font-semibold">Ticket #</th>
                <th className="px-md py-sm font-label-md text-label-md text-on-surface-variant font-semibold">Title</th>
                <th className="px-md py-sm font-label-md text-label-md text-on-surface-variant font-semibold">Customer</th>
                <th className="px-md py-sm font-label-md text-label-md text-on-surface-variant font-semibold">Status</th>
                <th className="px-md py-sm font-label-md text-label-md text-on-surface-variant font-semibold">Priority</th>
                <th className="px-md py-sm font-label-md text-label-md text-on-surface-variant font-semibold">Mode</th>
                <th className="px-md py-sm font-label-md text-label-md text-on-surface-variant font-semibold">Created Date</th>
              </tr>
            </thead>
            <tbody className="font-body-sm text-body-sm">
              {recent.map((t) => {
                const getAiBadge = () => {
                  if (t.waitingForAgent) {
                    return <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-semibold bg-amber-100 text-amber-700 uppercase tracking-wider"><span className="material-symbols-outlined text-[10px]">schedule</span>Waiting</span>;
                  }
                  if (t.humanAgentActive) {
                    return <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-semibold bg-blue-100 text-blue-700 uppercase tracking-wider"><span className="material-symbols-outlined text-[10px]">person</span>Human</span>;
                  }
                  if (t.aiModeEnabled) {
                    return <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-semibold bg-emerald-100 text-emerald-700 uppercase tracking-wider"><span className="material-symbols-outlined text-[10px]">smart_toy</span>AI</span>;
                  }
                  return <span className="text-on-surface-variant text-xs">â€”</span>;
                };

                return (
                  <tr key={t.id} className="border-b border-outline-variant hover:bg-surface-container-low transition-colors cursor-pointer group" onClick={() => navigate(`/tickets/${t.id}`)}>
                    <td className="px-md py-md font-mono-sm text-mono-sm text-on-surface-variant">{t.ticketNumber}</td>
                    <td className="px-md py-md text-on-surface font-medium group-hover:text-primary transition-colors">
                      <Link to={`/tickets/${t.id}`}>{t.title}</Link>
                    </td>
                    <td className="px-md py-md text-on-surface-variant">{t.customerName}</td>
                    <td className="px-md py-md">
                      <span className={`inline-flex items-center px-2 py-1 rounded font-label-md text-label-md ${statusChip(t.status?.name)}`}>{t.status?.name}</span>
                    </td>
                    <td className="px-md py-md">
                      <span className={`inline-flex items-center px-2 py-1 rounded font-label-md text-label-md ${prioChip(t.priority?.name)}`}>{t.priority?.name}</span>
                    </td>
                    <td className="px-md py-md">{getAiBadge()}</td>
                    <td className="px-md py-md text-on-surface-variant">{new Date(t.createdAt).toLocaleString()}</td>
                  </tr>
                );
              })}
              {recent.length === 0 && (
                <tr className="border-b border-outline-variant">
                  <td className="px-md py-md text-on-surface-variant" colSpan="7">No tickets yet.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}

