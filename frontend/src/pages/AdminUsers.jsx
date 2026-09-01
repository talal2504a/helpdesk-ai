import React, { useEffect, useState } from 'react';
import { api, createAgent } from '../api.js';
import AdminTabs from '../components/AdminTabs.jsx';
import Loading, { BtnSpinner } from '../components/Spinner.jsx';

const emptyForm = { name: '', email: '', password: '', role: 'Customer', departmentId: '', isActive: true };

export default function AdminUsers() {
  const [users, setUsers] = useState([]);
  const [departments, setDepartments] = useState([]);
  const [err, setErr] = useState('');
  const [search, setSearch] = useState('');
  const [modal, setModal] = useState(null); // null | { mode: 'add' | 'edit' | 'agent', user }
  const [form, setForm] = useState(emptyForm);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [agentResult, setAgentResult] = useState(null);

  const load = () => api.get('/users').then(({ data }) => setUsers(data)).catch((e) => setErr(e.message)).finally(() => setLoading(false));
  useEffect(() => { load(); api.get('/departments').then(({ data }) => setDepartments(data)).catch(() => {}); }, []);

  const openAdd = () => { setForm(emptyForm); setModal({ mode: 'add' }); setAgentResult(null); };
  const openAddAgent = () => { setForm({ name: '', email: '', departmentId: '' }); setModal({ mode: 'agent' }); setAgentResult(null); };
  const openEdit = (u) => {
    setForm({ name: u.name, email: u.email, password: '', role: u.role, departmentId: u.departmentId || '', isActive: u.isActive });
    setModal({ mode: 'edit', user: u });
  };

  const save = async (e) => {
    e.preventDefault();
    setBusy(true); setErr('');
    try {
      if (modal.mode === 'agent') {
        const payload = {
          name: form.name,
          email: form.email,
          departmentId: form.departmentId ? Number(form.departmentId) : null
        };
        const { data } = await createAgent(payload);
        setAgentResult(data);
        load();
      } else {
        const payload = {
          name: form.name, email: form.email, role: form.role,
          departmentId: form.departmentId ? Number(form.departmentId) : null,
          isActive: form.isActive,
          ...(form.password ? { password: form.password } : {}),
        };
        if (modal.mode === 'add') await api.post('/users', payload);
        else await api.put(`/users/${modal.user.id}`, payload);
        setModal(null);
      }
    } catch (ex) { setErr(ex.message); } finally { setBusy(false); }
  };

  const toggleActive = async (u) => {
    setErr('');
    try { await api.put(`/users/${u.id}`, { isActive: !u.isActive }); load(); }
    catch (ex) { setErr(ex.message); }
  };

  const remove = async (u) => {
    if (!window.confirm(`Deactivate "${u.name}"? Wo login nahi kar payenge.`)) return;
    setErr('');
    try { await api.delete(`/users/${u.id}`); load(); }
    catch (ex) { setErr(ex.message); }
  };

  const visible = users.filter((u) =>
    !search.trim() ||
    u.name?.toLowerCase().includes(search.toLowerCase()) ||
    u.email?.toLowerCase().includes(search.toLowerCase())
  );

  return (
    <div className="pt-16 p-lg">
      <AdminTabs />
      <div className="flex justify-between items-center mb-lg">
        <div>
          <h1 className="font-display-lg text-display-lg text-on-background">Users Management</h1>
          <p className="font-body-md text-body-md text-on-surface-variant mt-sm">Manage system access, roles, and permissions.</p>
        </div>
        <div className="flex gap-sm">
          <button onClick={openAddAgent} className="bg-emerald-600 text-white px-md py-sm rounded-lg font-label-md text-label-md flex items-center gap-sm hover:bg-emerald-700 transition-colors">
            <span className="material-symbols-outlined text-[20px]">support_agent</span>
            Create Agent
          </button>
          <button onClick={openAdd} className="bg-primary-container text-on-primary-container px-md py-sm rounded-lg font-label-md text-label-md flex items-center gap-sm hover:opacity-90 transition-opacity">
            <span className="material-symbols-outlined text-[20px]">person_add</span>
            Add New User
          </button>
        </div>
      </div>

      {/* Search bar */}
      <div className="bg-surface-container-lowest border border-outline-variant rounded-lg p-md mb-lg flex gap-md items-center">
        <div className="relative flex-1">
          <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant text-[20px]">search</span>
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full pl-10 pr-4 py-2 bg-surface border border-outline-variant rounded-lg focus:border-primary focus:ring-2 focus:ring-primary/10 transition-all font-body-md text-body-md outline-none"
            placeholder="Search users by name or email..."
            type="text"
          />
        </div>
      </div>

      {err && <div className="error mb-lg">{err}</div>}

      <div className="bg-surface-container-lowest border border-outline-variant rounded-lg overflow-hidden shadow-sm">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead className="bg-surface-container-low border-b border-outline-variant">
              <tr>
                <th className="p-md font-label-md text-label-md text-on-surface-variant font-semibold">User</th>
                <th className="p-md font-label-md text-label-md text-on-surface-variant font-semibold">Email</th>
                <th className="p-md font-label-md text-label-md text-on-surface-variant font-semibold">Role</th>
                <th className="p-md font-label-md text-label-md text-on-surface-variant font-semibold">Status</th>
                <th className="p-md font-label-md text-label-md text-on-surface-variant font-semibold text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="font-body-md text-body-md">
              {visible.map((u) => (
                <tr key={u.id} className="border-b border-outline-variant hover:bg-surface-container-low/50 transition-colors group">
                  <td className="p-md flex items-center gap-sm">
                    <div className="w-8 h-8 rounded-full bg-surface-variant border border-outline-variant flex items-center justify-center">
                      <span className="text-sm font-semibold text-on-surface">
                        {u.name?.split(' ').map(n => n[0]).join('').toUpperCase() || 'U'}
                      </span>
                    </div>
                    <span className={`font-semibold ${u.isActive ? 'text-on-surface' : 'text-on-surface-variant line-through'}`}>{u.name}</span>
                  </td>
                  <td className="p-md text-on-surface-variant">{u.email}</td>
                  <td className="p-md">
                    <span className={`inline-flex items-center px-2 py-1 rounded text-xs font-semibold ${
                      u.role === 'Admin' ? 'bg-rose-100 text-rose-800' :
                      u.role === 'Agent' ? 'bg-indigo-100 text-indigo-800' :
                      'bg-emerald-100 text-emerald-800'
                    }`}>{u.role}</span>
                  </td>
                  <td className="p-md">
                    <label className="relative inline-flex items-center cursor-pointer" title={u.isActive ? 'Active' : 'Inactive'}>
                      <input checked={u.isActive} className="sr-only peer" type="checkbox" onChange={() => toggleActive(u)} />
                      <div className="w-9 h-5 bg-outline-variant peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-4 after:w-4 after:transition-all peer-checked:bg-primary-container"></div>
                    </label>
                  </td>
                  <td className="p-md text-right">
                    <div className="flex justify-end gap-sm transition-opacity">
                      <button onClick={() => openEdit(u)} className="p-1 text-on-surface-variant hover:text-primary transition-colors" title="Edit user">
                        <span className="material-symbols-outlined text-[20px]">edit</span>
                      </button>
                      <button onClick={() => remove(u)} className="p-1 text-on-surface-variant hover:text-error transition-colors" title="Deactivate user">
                        <span className="material-symbols-outlined text-[20px]">delete</span>
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
              {loading && (
                <tr><td colSpan="5"><Loading label="Loading users…" /></td></tr>
              )}
              {!loading && visible.length === 0 && (
                <tr><td colSpan="5" className="p-md text-on-surface-variant text-center">No users found.</td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
      {/* Add / Edit User Modal */}
      {modal && (
        <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center p-md" onClick={() => setModal(null)}>
          <div className="bg-surface-container-lowest border border-outline-variant rounded-xl p-xl shadow-level-2 w-full max-w-md" onClick={(e) => e.stopPropagation()}>
            <h2 className="font-headline-md text-headline-md text-on-surface mb-lg">
              {modal.mode === 'agent' ? 'Create New Agent Account' : modal.mode === 'add' ? 'Add New User' : `Edit — ${modal.user.name}`}
            </h2>
            
            {agentResult ? (
              <div className="bg-emerald-50 border border-emerald-200 rounded-lg p-md mb-md">
                <h3 className="font-label-md text-label-md text-emerald-800 mb-sm flex items-center gap-sm">
                  <span className="material-symbols-outlined text-[20px]">check_circle</span>
                  Agent Account Created Successfully!
                </h3>
                <div className="space-y-sm text-body-sm">
                  <p><strong>Name:</strong> {agentResult.name}</p>
                  <p><strong>Email:</strong> {agentResult.email}</p>
                  <p><strong>Role:</strong> {agentResult.role}</p>
                  <p><strong>Department:</strong> {agentResult.department || 'None'}</p>
                  <div className="bg-white border border-emerald-200 rounded p-sm mt-sm">
                    <p className="font-semibold text-emerald-800">Generated Password:</p>
                    <code className="block bg-gray-100 p-2 rounded mt-1 font-mono text-sm break-all">{agentResult.generatedPassword}</code>
                  </div>
                  <p className="text-xs text-on-surface-variant">Share this password with the agent. They can login at: {window.location.origin}/login</p>
                </div>
                <button onClick={() => { setModal(null); setAgentResult(null); }}
                  className="mt-md w-full py-2 bg-emerald-600 text-white rounded-lg font-label-md text-label-md hover:bg-emerald-700 transition-colors">
                  Done
                </button>
              </div>
            ) : (
              <form onSubmit={save} className="flex flex-col gap-md">
                <div>
                  <label className="block font-label-md text-label-md text-on-surface mb-sm">Full Name</label>
                  <input required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })}
                    className="w-full px-md py-sm bg-surface-container-lowest border border-outline-variant rounded-lg font-body-md text-body-md focus:border-primary focus:ring-2 focus:ring-primary/10 outline-none" />
                </div>
                <div>
                  <label className="block font-label-md text-label-md text-on-surface mb-sm">Email</label>
                  <input required type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })}
                    className="w-full px-md py-sm bg-surface-container-lowest border border-outline-variant rounded-lg font-body-md text-body-md focus:border-primary focus:ring-2 focus:ring-primary/10 outline-none" />
                </div>
                <div>
                  <label className="block font-label-md text-label-md text-on-surface mb-sm">Department</label>
                  <select value={form.departmentId} onChange={(e) => setForm({ ...form, departmentId: e.target.value })}
                    className="w-full px-md py-sm bg-surface-container-lowest border border-outline-variant rounded-lg font-body-md text-body-md focus:border-primary outline-none">
                    <option value="">None</option>
                    {departments.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
                  </select>
                </div>
                {modal.mode !== 'agent' && (
                  <>
                    <div>
                      <label className="block font-label-md text-label-md text-on-surface mb-sm">
                        Password {modal.mode === 'edit' && <span className="text-on-surface-variant font-normal">(blank = no change)</span>}
                      </label>
                      <input required={modal.mode === 'add'} type="password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })}
                        className="w-full px-md py-sm bg-surface-container-lowest border border-outline-variant rounded-lg font-body-md text-body-md focus:border-primary focus:ring-2 focus:ring-primary/10 outline-none" />
                    </div>
                    <div className="grid grid-cols-2 gap-md">
                      <div>
                        <label className="block font-label-md text-label-md text-on-surface mb-sm">Role</label>
                        <select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value })}
                          className="w-full px-md py-sm bg-surface-container-lowest border border-outline-variant rounded-lg font-body-md text-body-md focus:border-primary outline-none">
                          <option>Customer</option>
                          <option>Agent</option>
                          <option>Admin</option>
                        </select>
                      </div>
                    </div>
                    <label className="flex items-center gap-sm cursor-pointer">
                      <input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
                        className="rounded border-outline-variant text-primary focus:ring-primary/20 cursor-pointer" />
                      <span className="font-body-sm text-body-sm text-on-surface-variant">Active (login allowed)</span>
                    </label>
                  </>
                )}
                {err && <div className="error">{err}</div>}
                <div className="flex justify-end gap-sm mt-sm">
                  <button type="button" onClick={() => { setModal(null); setAgentResult(null); }}
                    className="px-md py-sm bg-surface border border-outline-variant rounded-lg font-label-md text-label-md text-on-surface hover:bg-surface-container-low transition-colors">
                    Cancel
                  </button>
                  <button type="submit" disabled={busy}
                    className="px-md py-sm bg-primary text-on-primary rounded-lg font-label-md text-label-md hover:opacity-90 transition-opacity disabled:opacity-50 flex items-center gap-sm">
                    {busy && <BtnSpinner />}
                    {modal.mode === 'agent' ? 'Create Agent' : modal.mode === 'add' ? 'Create User' : 'Save Changes'}
                    <span className="material-symbols-outlined text-[16px]">check</span>
                  </button>
                </div>
              </form>
            )}
          </div>
        </div>
      )}

    </div>
  );
}