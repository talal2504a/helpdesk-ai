import React, { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../AuthContext.jsx';
import {
  startHub, stopHub,
  onHubMessage, onHubTicket, onHubNotification,
  onAiStatusChanged, onHumanAgentJoined,
} from '../api.js';

export default function Layout({ children }) {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [query, setQuery] = useState('');
  const [notifs, setNotifs] = useState([]);
  const [notifOpen, setNotifOpen] = useState(false);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const notifRef = useRef(null);
  const menuRef = useRef(null);
  const menuBtnRef = useRef(null);

  const staff = user && user.role !== 'Customer';

  useEffect(() => {
    if (!user) return;
    startHub();

    const push = (title, body, ticketId) => {
      setNotifs((prev) => [{ id: Date.now() + Math.random(), title, body, ticketId, at: new Date() }, ...prev].slice(0, 20));
    };

    const unMsg = onHubMessage((m) => {
      // Skip own messages — polling delivers them too, but own sends already reload the view.
      if (m.senderId && user && String(m.senderId) === String(user.id)) return;
      push('💬 New message on a ticket', m.senderName ? `${m.senderName}: ${m.content || m.body || ''}` : (m.content || m.body || ''), m.ticketId);
    });
    const unTic = onHubTicket((u) => {
      const ticketId = u && (u.ticketId || (u.payload && u.payload.ticketId));
      push('🔄 Ticket updated', (u && u.action) || 'Ticket updated', ticketId);
    });
    const unNot = onHubNotification((n) => {
      const ticketId = n.data && n.data.ticketId;
      push(n.title || '🔔 Notification', n.body || n.message || '', ticketId);
    });
    const unAi = onAiStatusChanged((data) => {
      push(data.enabled ? '🤖 AI mode enabled' : '🤖 AI mode disabled', data.ticketId ? `Ticket #${data.ticketId}` : '', data.ticketId);
    });
    const unHuman = onHumanAgentJoined((data) => {
      push('👤 Human agent joined', data.agentName ? `${data.agentName} joined the conversation` : '', data.ticketId);
    });

    return () => { unMsg(); unTic(); unNot(); unAi(); unHuman(); stopHub(); };
  }, [user]);

  useEffect(() => {
    const handler = (e) => {
      // Hamburger/X button click ko ignore karo — button ka apna toggle handler chalega
      if (menuBtnRef.current && menuBtnRef.current.contains(e.target)) return;
      if (notifRef.current && !notifRef.current.contains(e.target)) setNotifOpen(false);
      if (menuRef.current && !menuRef.current.contains(e.target)) setMobileMenuOpen(false);
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  useEffect(() => {
    setMobileMenuOpen(false);
  }, [location.pathname]);

  const isActive = (path) => location.pathname === path;

  const handleLogout = () => { logout(); navigate('/login'); };

  const handleSearch = (e) => {
    e.preventDefault();
    navigate(`/tickets${query.trim() ? `?search=${encodeURIComponent(query.trim())}` : ''}`);
  };

  const linkCls = (active) => `font-label-md text-label-md py-2 px-3 rounded-md transition-colors ${
    active ? 'text-primary font-bold border-b-2 border-primary' : 'text-on-surface-variant hover:bg-surface-container-low'
  }`;

  const mobileLinkCls = (active) => `font-label-md text-label-md py-3 px-4 rounded-lg transition-colors ${
    active ? 'text-primary font-bold bg-primary/5' : 'text-on-surface-variant hover:bg-surface-container-low'
  }`;

  return (
    <div className="min-h-screen bg-surface flex flex-col">
      <nav className="bg-surface border-b border-outline-variant fixed top-0 w-full z-50 flex justify-between items-center px-md sm:px-lg py-sm h-16 overflow-hidden">
        <div className="flex items-center gap-xl min-w-0">
          <Link to="/" className="font-headline-lg text-headline-lg font-bold text-primary flex items-center gap-sm anim-wiggle-hover cursor-pointer shrink-0">
            <span className="material-symbols-outlined fill text-[28px]">support_agent</span>
            <span className="hidden sm:inline">HelpDesk</span>
          </Link>

          {user && (
            <div className="hidden md:flex gap-lg ml-xl">
              {staff && <Link to="/dashboard" className={linkCls(isActive('/dashboard'))}>Dashboard</Link>}
              <Link
                to={staff ? '/tickets' : '/'}
                className={linkCls(staff ? isActive('/tickets') || location.pathname.startsWith('/tickets/') : isActive('/'))}
              >
                My Tickets
              </Link>
              {staff && <Link to="/queue" className={linkCls(isActive('/queue'))}>My Queue</Link>}
              {user.role === 'Admin' && (
                <Link to="/admin/users" className={linkCls(location.pathname.startsWith('/admin'))}>Admin Panel</Link>
              )}
            </div>
          )}
        </div>

        <div className="flex items-center gap-sm md:gap-md">
          {user && (
            <>
              <form onSubmit={handleSearch} className="relative hidden sm:block">
                <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-outline-variant text-[20px]">search</span>
                <input
                  className="pl-10 pr-4 py-1.5 bg-surface-container-lowest border border-outline-variant rounded-lg text-body-sm focus:border-primary focus:ring-2 focus:ring-primary/10 w-36 sm:w-44 md:w-64 transition-all"
                  placeholder="Search tickets..."
                  type="text"
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                />
              </form>

              <div className="relative shrink-0" ref={notifRef}>
                <button
                  onClick={() => setNotifOpen((o) => !o)}
                  className="p-2 text-on-surface-variant hover:bg-surface-container-low rounded-full transition-colors relative"
                  title="Notifications"
                >
                  <span className="material-symbols-outlined text-[20px]">notifications</span>
                  {notifs.length > 0 && <span className="absolute top-1.5 right-1.5 w-2 h-2 bg-error rounded-full animate-pulse"></span>}
                </button>
                {notifOpen && (
                  <div className="absolute right-0 mt-2 w-72 sm:w-80 max-w-[calc(100vw-2rem)] bg-surface-container-lowest border border-outline-variant rounded-lg shadow-level-2 z-50 overflow-hidden anim-slide-down origin-top-right">
                    <div className="flex justify-between items-center px-md py-sm border-b border-outline-variant bg-surface-bright">
                      <span className="font-label-md text-label-md text-on-surface">Notifications</span>
                      {notifs.length > 0 && (
                        <button onClick={() => setNotifs([])} className="font-label-md text-label-md text-primary hover:underline">Clear all</button>
                      )}
                    </div>
                    <div className="max-h-72 overflow-y-auto msg-scroll">
                      {notifs.length === 0 && (
                        <div className="p-md text-center text-on-surface-variant font-body-sm text-body-sm">No new notifications.</div>
                      )}
                      {notifs.map((n) => (
                        <button
                          key={n.id}
                          type="button"
                          onClick={() => {
                            if (n.ticketId) navigate(`/tickets/${n.ticketId}`);
                            setNotifOpen(false);
                          }}
                          className="w-full text-left px-md py-sm border-b border-outline-variant/50 hover:bg-surface-container-low transition-colors"
                        >
                          <div className="font-body-sm text-body-sm font-medium text-on-surface">{n.title}</div>
                          {n.body && <div className="font-body-sm text-body-sm text-on-surface-variant">{n.body}</div>}
                          <div className="text-[11px] text-on-surface-variant mt-0.5">{n.at.toLocaleTimeString()}</div>
                        </button>
                      ))}
                    </div>
                  </div>
                )}
              </div>

              <div className="hidden md:flex items-center gap-sm">
                <div className="w-8 h-8 rounded-full bg-surface-variant border border-outline-variant flex items-center justify-center">
                  <span className="text-sm font-semibold text-on-surface">
                    {user.name?.split(' ').map(n => n[0]).join('').toUpperCase() || 'U'}
                  </span>
                </div>
                <div className="hidden lg:block">
                  <div className="text-sm font-medium text-on-surface">{user.name}</div>
                  <div className="text-xs text-on-surface-variant">{user.role}</div>
                </div>
                <button
                  onClick={handleLogout}
                  className="p-2 text-on-surface-variant hover:bg-surface-container-low rounded-full transition-colors"
                  title="Logout"
                >
                  <span className="material-symbols-outlined text-[20px]">logout</span>
                </button>
              </div>

              {user && (
                <button
                  ref={menuBtnRef}
                  onClick={() => setMobileMenuOpen((o) => !o)}
                  className="md:hidden p-2 shrink-0 text-on-surface-variant hover:bg-surface-container-low rounded-full transition-colors"
                  title="Menu"
                  aria-label={mobileMenuOpen ? 'Close menu' : 'Open menu'}
                >
                  <span className="material-symbols-outlined text-[24px]">{mobileMenuOpen ? 'close' : 'menu'}</span>
                </button>
              )}
            </>
          )}
        </div>
      </nav>

      {mobileMenuOpen && user && (
        <div className="md:hidden fixed top-16 left-0 right-0 bottom-0 bg-surface z-40 overflow-y-auto" ref={menuRef}>
          <div className="p-lg space-y-2">
            <form onSubmit={handleSearch} className="relative mb-4 sm:hidden">
              <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-outline-variant text-[20px]">search</span>
              <input
                className="w-full pl-10 pr-4 py-2 bg-surface-container-lowest border border-outline-variant rounded-lg text-body-sm focus:border-primary focus:ring-2 focus:ring-primary/10"
                placeholder="Search tickets..."
                type="text"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
              />
            </form>

            {staff && (
              <Link to="/dashboard" className={`block ${mobileLinkCls(isActive('/dashboard'))}`}>
                <span className="material-symbols-outlined text-[18px] mr-2">dashboard</span>
                Dashboard
              </Link>
            )}
            <Link
              to={staff ? '/tickets' : '/'}
              className={`block ${mobileLinkCls(staff ? isActive('/tickets') || location.pathname.startsWith('/tickets/') : isActive('/'))}`}
            >
              <span className="material-symbols-outlined text-[18px] mr-2">confirmation_number</span>
              My Tickets
            </Link>
            {staff && (
              <Link to="/queue" className={`block ${mobileLinkCls(isActive('/queue'))}`}>
                <span className="material-symbols-outlined text-[18px] mr-2">queue</span>
                My Queue
              </Link>
            )}
            {user.role === 'Admin' && (
              <Link to="/admin/users" className={`block ${mobileLinkCls(location.pathname.startsWith('/admin'))}`}>
                <span className="material-symbols-outlined text-[18px] mr-2">admin_panel_settings</span>
                Admin Panel
              </Link>
            )}

            <div className="border-t border-outline-variant pt-4 mt-4">
              <div className="flex items-center gap-3 mb-4">
                <div className="w-10 h-10 rounded-full bg-surface-variant border border-outline-variant flex items-center justify-center">
                  <span className="text-sm font-semibold text-on-surface">
                    {user.name?.split(' ').map(n => n[0]).join('').toUpperCase() || 'U'}
                  </span>
                </div>
                <div>
                  <div className="text-sm font-medium text-on-surface">{user.name}</div>
                  <div className="text-xs text-on-surface-variant">{user.role}</div>
                </div>
              </div>
              <button
                onClick={handleLogout}
                className="w-full flex items-center gap-2 py-3 px-4 text-on-surface-variant hover:bg-surface-container-low rounded-lg transition-colors"
              >
                <span className="material-symbols-outlined text-[18px]">logout</span>
                Logout
              </button>
            </div>
          </div>
        </div>
      )}

      <main className="flex-1 pt-16">
        <div key={location.pathname} className="anim-fade-in">
          {children}
        </div>
      </main>
    </div>
  );
}
