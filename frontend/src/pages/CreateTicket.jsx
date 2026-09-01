import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api.js';
import { BtnSpinner } from '../components/Spinner.jsx';

export default function CreateTicket() {
  const nav = useNavigate();
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [categoryId, setCategoryId] = useState('');
  const [priorityId, setPriorityId] = useState('');
  const [categories, setCategories] = useState([]);
  const [priorities, setPriorities] = useState([]);
  const [err, setErr] = useState('');
  const [busy, setBusy] = useState(false);
  const [success, setSuccess] = useState(null);
  const [titleTouched, setTitleTouched] = useState(false);
  const [descTouched, setDescTouched] = useState(false);

  useEffect(() => { api.get('/categories').then(({ data }) => setCategories(data)).catch(() => {}); }, []);
  useEffect(() => { api.get('/priorities').then(({ data }) => setPriorities(data)).catch(() => {}); }, []);

  const titleValid = title.trim().length >= 5 && title.length <= 200;
  const descValid = description.trim().length >= 10 && description.length <= 8000;
  const formValid = titleValid && descValid;

  const submit = async (e) => {
    e.preventDefault();
    setTitleTouched(true);
    setDescTouched(true);
    if (!formValid) return;
    setBusy(true); setErr(''); setSuccess(null);
    try {
      const payload = { title, description, categoryId: categoryId ? Number(categoryId) : null, priorityId: priorityId ? Number(priorityId) : null };
      const { data } = await api.post('/tickets', payload);
      setSuccess({ id: data.id, ticketNumber: data.ticketNumber });
      setTimeout(() => nav(`/tickets/${data.id}`), 2000);
    } catch (ex) { setErr(ex.message); } finally { setBusy(false); }
  };

  return (
    <div className="max-w-2xl mx-auto px-4">
      <div className="mb-lg">
        <h1 className="font-headline-lg text-headline-lg text-on-background">Create New Ticket</h1>
        <p className="font-body-md text-body-md text-on-surface-variant mt-sm">Fill in the details below. AI will automatically classify if left blank.</p>
      </div>

      {success && (
        <div className="mb-lg bg-emerald-50 border border-emerald-200 rounded-xl p-md flex items-center gap-3">
          <span className="material-symbols-outlined text-emerald-600 text-[24px]">check_circle</span>
          <div>
            <p className="font-label-md text-label-md text-emerald-700">Ticket created successfully!</p>
            <p className="font-body-sm text-body-sm text-emerald-600">Ticket #{success.ticketNumber} has been created. Redirecting...</p>
          </div>
        </div>
      )}

      <div className="bg-surface-container-lowest border border-outline-variant rounded-xl p-xl shadow-level-2">
        <form onSubmit={submit} className="flex flex-col gap-lg">
          <div>
            <label className="block font-label-md text-label-md text-on-surface mb-sm" htmlFor="title">
              Title <span className="text-on-surface-variant font-normal">(5–200 characters)</span>
            </label>
            <input
              id="title"
              className={`w-full px-md py-sm bg-surface-container-lowest border rounded-lg font-body-md text-body-md text-on-surface placeholder:text-outline focus:ring-2 transition-shadow outline-none ${
                titleTouched && !titleValid ? 'border-error focus:border-error focus:ring-error/10' : 'border-outline-variant focus:border-primary focus:ring-primary/10'
              }`}
              value={title}
              onChange={(e) => { setTitle(e.target.value); if (!titleTouched) setTitleTouched(true); }}
              onBlur={() => setTitleTouched(true)}
              placeholder="What's wrong?"
              required
              minLength={5}
              maxLength={200}
            />
            <div className="flex justify-between items-center mt-1">
              <p className={`font-body-sm text-body-sm ${
                titleTouched && !titleValid ? 'text-error' : 'text-on-surface-variant'
              }`}>
                {titleTouched && title.trim().length > 0 && title.trim().length < 5 && 'At least 5 characters needed'}
                {titleTouched && title.length > 200 && 'Maximum 200 characters'}
              </p>
              <p className={`font-body-sm text-body-sm ${
                title.length > 200 ? 'text-error' : title.length > 180 ? 'text-amber-600' : 'text-on-surface-variant'
              }`}>
                {title.length}/200
              </p>
            </div>
          </div>

          <div>
            <label className="block font-label-md text-label-md text-on-surface mb-sm" htmlFor="description">
              Description <span className="text-on-surface-variant font-normal">(10–8000 characters)</span>
            </label>
            <textarea
              id="description"
              rows="6"
              className={`w-full px-md py-sm bg-surface-container-lowest border rounded-lg font-body-md text-body-md text-on-surface placeholder:text-outline focus:ring-2 transition-shadow outline-none resize-none ${
                descTouched && !descValid ? 'border-error focus:border-error focus:ring-error/10' : 'border-outline-variant focus:border-primary focus:ring-primary/10'
              }`}
              value={description}
              onChange={(e) => { setDescription(e.target.value); if (!descTouched) setDescTouched(true); }}
              onBlur={() => setDescTouched(true)}
              placeholder="Describe the issue in detail (minimum 10 characters)…"
              required
              minLength={10}
              maxLength={8000}
            />
            <div className="flex justify-between items-center mt-1">
              <p className={`font-body-sm text-body-sm ${
                descTouched && !descValid ? 'text-error' : 'text-on-surface-variant'
              }`}>
                {descTouched && description.trim().length > 0 && description.trim().length < 10 && 'At least 10 characters needed'}
                {descTouched && description.length > 8000 && 'Maximum 8000 characters'}
              </p>
              <p className={`font-body-sm text-body-sm ${
                description.length > 8000 ? 'text-error' : description.length > 7500 ? 'text-amber-600' : 'text-on-surface-variant'
              }`}>
                {description.length}/8000
              </p>
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-md">
            <div>
              <label className="block font-label-md text-label-md text-on-surface mb-sm" htmlFor="category">Category</label>
              <select
                id="category"
                className="w-full px-md py-sm bg-surface-container-lowest border border-outline-variant rounded-lg font-body-md text-body-md text-on-surface focus:border-primary focus:ring-2 focus:ring-primary/10 outline-none"
                value={categoryId}
                onChange={(e) => setCategoryId(e.target.value)}
              >
                <option value="">Auto (AI)</option>
                {categories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </div>
            <div>
              <label className="block font-label-md text-label-md text-on-surface mb-sm" htmlFor="priority">Priority</label>
              <select
                id="priority"
                className="w-full px-md py-sm bg-surface-container-lowest border border-outline-variant rounded-lg font-body-md text-body-md text-on-surface focus:border-primary focus:ring-2 focus:ring-primary/10 outline-none"
                value={priorityId}
                onChange={(e) => setPriorityId(e.target.value)}
              >
                <option value="">Auto (AI)</option>
                {priorities.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
              </select>
            </div>
          </div>

          {err && <div className="error">{err}</div>}

          <button
            type="submit"
            disabled={busy || !formValid}
            className="w-full bg-primary text-on-primary font-headline-md text-headline-md py-sm rounded-lg hover:bg-primary-container transition-colors flex items-center justify-center gap-sm disabled:opacity-50"
          >
            {busy && <BtnSpinner />}
            {busy ? 'Submitting…' : 'Submit ticket'}
            {!busy && <span className="material-symbols-outlined text-[18px]">send</span>}
          </button>
        </form>
      </div>
    </div>
  );
}
