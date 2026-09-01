import React, { useEffect, useState } from 'react';
import { api } from '../api.js';
import AdminTabs from '../components/AdminTabs.jsx';
import Loading from '../components/Spinner.jsx';

function Section({ title, list, onAdd, onUpdate, onDelete, extraHeader }) {
  const [name, setName] = useState('');
  const [desc, setDesc] = useState('');
  const [level, setLevel] = useState('');
  const [err, setErr] = useState('');
  const [editId, setEditId] = useState(null);
  const [editName, setEditName] = useState('');
  const [editExtra, setEditExtra] = useState('');

  const startEdit = (item) => {
    setEditId(item.id);
    setEditName(item.name);
    setEditExtra(String(item.level ?? item.description ?? ''));
  };

  const saveEdit = async (item) => {
    setErr('');
    try {
      const payload = extraHeader
        ? { name: editName, description: null, level: Number(editExtra) || 1, isActive: true }
        : { name: editName, description: item.description, isActive: true };
      await onUpdate(item.id, payload);
      setEditId(null);
    } catch (ex) { setErr(ex.message); }
  };

  const submit = async (e) => {
    e.preventDefault();
    try {
      await onAdd({ name, description: desc || null, ...(level ? { level: Number(level) } : {}) });
      setName(''); setDesc(''); setLevel('');
    } catch (ex) { setErr(ex.message); }
  };
  return (
    <div className="bg-surface border border-outline-variant rounded-xl p-md shadow-sm">
      <h3 className="font-headline-md text-headline-md text-on-surface mb-3">{title}</h3>
      <div className="overflow-x-auto">
        <table className="w-full text-left border-collapse">
          <thead className="bg-surface-container-low border-b border-outline-variant">
            <tr>
              <th className="p-md font-label-md text-label-md text-on-surface-variant font-semibold">Name</th>
              {extraHeader && <th className="p-md font-label-md text-label-md text-on-surface-variant font-semibold">Level/Description</th>}
              <th className="p-md font-label-md text-label-md text-on-surface-variant font-semibold text-right">Actions</th>
            </tr>
          </thead>
          <tbody className="font-body-md text-body-md">
            {list.map((item) => (
              <tr key={item.id} className="border-b border-outline-variant hover:bg-surface-container-low/50 transition-colors group">
                <td className="p-md text-on-surface">
                  {editId === item.id ? (
                    <input value={editName} onChange={(e) => setEditName(e.target.value)} autoFocus
                      className="w-full px-sm py-1 bg-surface border border-primary rounded-lg font-body-md text-body-md outline-none" />
                  ) : item.name}
                </td>
                {extraHeader && (
                  <td className="p-md text-on-surface-variant">
                    {editId === item.id ? (
                      <input value={editExtra} onChange={(e) => setEditExtra(e.target.value)} type="number"
                        className="w-24 px-sm py-1 bg-surface border border-primary rounded-lg font-body-md text-body-md outline-none" />
                    ) : (item.level ?? item.description ?? '—')}
                  </td>
                )}
                <td className="p-md text-right">
                  <div className="flex justify-end gap-sm transition-opacity">
                    {editId === item.id ? (
                      <>
                        <button className="p-1 text-primary hover:text-primary-container transition-colors" title="Save" onClick={() => saveEdit(item)}>
                          <span className="material-symbols-outlined text-[20px]">check</span>
                        </button>
                        <button className="p-1 text-on-surface-variant hover:text-error transition-colors" title="Cancel" onClick={() => setEditId(null)}>
                          <span className="material-symbols-outlined text-[20px]">close</span>
                        </button>
                      </>
                    ) : (
                      <>
                        <button className="p-1 text-on-surface-variant hover:text-primary transition-colors" title="Edit" onClick={() => startEdit(item)}>
                          <span className="material-symbols-outlined text-[20px]">edit</span>
                        </button>
                        <button className="p-1 text-on-surface-variant hover:text-error transition-colors" title="Delete" onClick={() => onDelete(item.id).catch((ex) => setErr(ex.message))}>
                          <span className="material-symbols-outlined text-[20px]">delete</span>
                        </button>
                      </>
                    )}
                  </div>
                </td>
              </tr>
            ))}
            {!list.length && <tr><td colSpan="3" className="p-md text-on-surface-variant text-center">None yet.</td></tr>}
          </tbody>
        </table>
      </div>
      <form className="flex gap-md mt-md" onSubmit={submit}>
        <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Name" className="flex-1 px-md py-2 bg-surface border border-outline-variant rounded-lg font-body-md focus:border-primary focus:ring-2 focus:ring-primary/10 outline-none" />
        {extraHeader && <input value={level} onChange={(e) => setLevel(e.target.value)} placeholder="Level (1-10)" type="number" className="w-32 px-md py-2 bg-surface border border-outline-variant rounded-lg font-body-md focus:border-primary focus:ring-2 focus:ring-primary/10 outline-none" />}
        <button type="submit" className="px-md py-2 bg-primary-container text-on-primary-container rounded-lg font-label-md text-label-md hover:opacity-90 transition-opacity">Add</button>
      </form>
      {err && <div className="error mt-sm">{err}</div>}
    </div>
  );
}

export default function AdminLookups() {
  const [tabs, setTabs] = useState('categories');
  const [loaded, setLoaded] = useState(false);
  const [categories, setCategories] = useState([]);
  const [priorities, setPriorities] = useState([]);
  const [statuses, setStatuses] = useState([]);
  const [departments, setDepartments] = useState([]);

  const load = (kind) => api.get('/' + kind).then(({ data }) => {
    if (kind === 'categories') setCategories(data);
    else if (kind === 'priorities') setPriorities(data);
    else if (kind === 'statuses') setStatuses(data);
    else setDepartments(data);
  }).catch(() => {});

  useEffect(() => {
    Promise.all(['categories', 'priorities', 'statuses', 'departments'].map(load)).finally(() => setLoaded(true));
  }, []);

  const end = (kind) => `/${kind}`;
  const add = (kind) => (payload) => api.post('/admin/' + kind, payload).then(() => load(kind));
  const upd = (kind) => (id, payload) => api.put('/admin/' + kind + '/' + id, payload).then(() => load(kind));
  const del = (kind) => (id) => api.delete('/admin/' + kind + '/' + id).then(() => load(kind));

  const tab = (key, label) => <button key={key} className={`font-label-md text-label-md px-md py-sm rounded-lg transition-colors ${tabs === key ? 'bg-primary-container text-on-primary-container' : 'text-on-surface-variant hover:bg-surface-container-low'}`} onClick={() => setTabs(key)}>{label}</button>;

  return (
    <div className="pt-16 p-lg">
      <AdminTabs />
      <h1 className="font-display-lg text-display-lg text-on-background mb-lg">Master Data</h1>
      <div className="flex gap-sm mb-lg">{tab('categories', 'Categories')}{tab('priorities', 'Priorities')}{tab('statuses', 'Statuses')}{tab('departments', 'Departments')}</div>
      {!loaded ? (
        <Loading label="Loading master data…" />
      ) : (
      <div>
        {tabs === 'categories' && <Section title="Categories" list={categories} onAdd={add('categories')} onUpdate={upd('categories')} onDelete={del('categories')} />}
        {tabs === 'priorities' && <Section title="Priorities" list={priorities} onAdd={add('priorities')} onUpdate={upd('priorities')} onDelete={del('priorities')} extraHeader />}
        {tabs === 'statuses' && <Section title="Statuses" list={statuses} onAdd={add('statuses')} onUpdate={upd('statuses')} onDelete={del('statuses')} />}
        {tabs === 'departments' && <Section title="Departments" list={departments} onAdd={add('departments')} onUpdate={upd('departments')} onDelete={del('departments')} />}
      </div>
      )}

    </div>
  );
}