import React from 'react';
import { Link, useLocation } from 'react-router-dom';

/// Admin section ke andar Users ⇄ Master Data tabs
export default function AdminTabs() {
  const { pathname } = useLocation();
  const tab = (to, label, icon) => (
    <Link
      to={to}
      className={`flex items-center gap-sm font-label-md text-label-md px-md py-sm rounded-lg transition-colors ${
        pathname === to ? 'bg-primary-container text-on-primary-container' : 'text-on-surface-variant hover:bg-surface-container-low'
      }`}
    >
      <span className="material-symbols-outlined text-[18px]">{icon}</span>
      {label}
    </Link>
  );
  return (
    <div className="flex gap-sm mb-lg">
      {tab('/admin/users', 'Users Management', 'people')}
      {tab('/admin/lookups', 'Master Data', 'tune')}
    </div>
  );
}
