import React, { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { api } from '../api.js';
import { BtnSpinner } from '../components/Spinner.jsx';

export default function Register() {
  const navigate = useNavigate();
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const getStrength = (val) => {
    if (!val || val.length < 6) return { score: 0, label: 'Weak', cls: 'bg-surface-container-high', textCls: 'text-error' };
    if (val.length < 10) return { score: 1, label: 'Fair', cls: 'bg-tertiary-container', textCls: 'text-tertiary-container' };
    if (/[A-Z]/.test(val) && /[0-9]/.test(val)) return { score: 3, label: 'Strong', cls: 'bg-secondary', textCls: 'text-secondary' };
    return { score: 2, label: 'Good', cls: 'bg-primary', textCls: 'text-primary' };
  };
  const strength = getStrength(password);

  const submit = async (e) => {
    e.preventDefault();
    setLoading(true); setError('');
    if (password !== confirmPassword) { setError('Passwords do not match'); setLoading(false); return; }
    try {
      await api.post('/auth/register', { name, email, password });
      navigate('/login');
    } catch (err) { setError(err.message); } finally { setLoading(false); }
  };

  return (
    <div className="min-h-screen bg-surface flex items-center justify-center p-md">
      <main className="w-full max-w-[440px]">
        <div className="text-center mb-xl">
          <div className="inline-flex items-center justify-center w-12 h-12 bg-primary rounded-xl mb-md anim-floaty anim-wiggle-hover cursor-pointer">
            <span className="material-symbols-outlined text-on-primary text-[28px] fill">headset_mic</span>
          </div>
          <h1 className="font-headline-lg text-headline-lg text-primary">HelpDesk</h1>
          <p className="font-body-md text-body-md text-on-surface-variant mt-sm">Create an account to get started</p>
        </div>

        <div className="bg-surface-container-lowest border border-outline-variant rounded-xl p-xl shadow-level-2 anim-pop">
          <form onSubmit={submit} className="flex flex-col gap-lg">
            <div>
              <label className="block font-label-md text-label-md text-on-surface mb-sm" htmlFor="fullName">Full Name</label>
              <div className="relative">
                <span className="absolute inset-y-0 left-0 flex items-center pl-md text-outline">
                  <span className="material-symbols-outlined">person</span>
                </span>
                <input 
                  className="w-full pl-xl pr-md py-sm bg-surface-container-lowest border border-outline-variant rounded-lg font-body-md text-body-md text-on-surface placeholder:text-outline focus:border-primary focus:ring-2 focus:ring-primary/10 transition-shadow outline-none"
                  id="fullName" 
                  type="text" 
                  placeholder="Jane Doe" 
                  required 
                  value={name} 
                  onChange={(e) => setName(e.target.value)} 
                />
              </div>
            </div>

            <div>
              <label className="block font-label-md text-label-md text-on-surface mb-sm" htmlFor="email">Email Address</label>
              <div className="relative">
                <span className="absolute inset-y-0 left-0 flex items-center pl-md text-outline">
                  <span className="material-symbols-outlined">mail</span>
                </span>
                <input 
                  className="w-full pl-xl pr-md py-sm bg-surface-container-lowest border border-outline-variant rounded-lg font-body-md text-body-md text-on-surface placeholder:text-outline focus:border-primary focus:ring-2 focus:ring-primary/10 transition-shadow outline-none"
                  id="email" 
                  type="email" 
                  placeholder="jane.doe@example.com" 
                  required 
                  value={email} 
                  onChange={(e) => setEmail(e.target.value)} 
                />
              </div>
            </div>

            <div>
              <label className="block font-label-md text-label-md text-on-surface mb-sm" htmlFor="password">Password</label>
              <div className="relative">
                <span className="absolute inset-y-0 left-0 flex items-center pl-md text-outline">
                  <span className="material-symbols-outlined">lock</span>
                </span>
                <input 
                  className="w-full pl-xl pr-md py-sm bg-surface-container-lowest border border-outline-variant rounded-lg font-body-md text-body-md text-on-surface placeholder:text-outline focus:border-primary focus:ring-2 focus:ring-primary/10 transition-shadow outline-none"
                  id="password" 
                  type="password" 
                  placeholder="••••••••" 
                  required 
                  value={password} 
                  onChange={(e) => setPassword(e.target.value)} 
                />
              </div>
              {/* Password Strength Meter (matches register_helpdesk design) */}
              <div className="mt-sm">
                <div className="flex gap-1">
                  {[0, 1, 2, 3].map((i) => (
                    <div key={i} className={`w-1/4 h-1 rounded-full ${i <= strength.score ? strength.cls : 'bg-surface-container-high'}`}></div>
                  ))}
                </div>
                <span className={`font-body-sm text-body-sm font-semibold ${strength.textCls}`}>{strength.label}</span>
              </div>
            </div>

            <div>
              <label className="block font-label-md text-label-md text-on-surface mb-sm" htmlFor="confirmPassword">Confirm Password</label>
              <div className="relative">
                <span className="absolute inset-y-0 left-0 flex items-center pl-md text-outline">
                  <span className="material-symbols-outlined">lock_reset</span>
                </span>
                <input 
                  className="w-full pl-xl pr-md py-sm bg-surface-container-lowest border border-outline-variant rounded-lg font-body-md text-body-md text-on-surface placeholder:text-outline focus:border-primary focus:ring-2 focus:ring-primary/10 transition-shadow outline-none"
                  id="confirmPassword" 
                  type="password" 
                  placeholder="••••••••" 
                  required 
                  value={confirmPassword} 
                  onChange={(e) => setConfirmPassword(e.target.value)} 
                />
              </div>
            </div>

            {error && <div className="error">{error}</div>}

            <button 
              type="submit" 
              disabled={loading}
              className="w-full bg-primary text-on-primary font-headline-md text-headline-md py-sm rounded-lg hover:bg-primary-container transition-colors flex items-center justify-center gap-sm mt-sm disabled:opacity-50"
            >
              {loading && <BtnSpinner />}
              {loading ? 'Creating account…' : 'Register'}
              {!loading && <span className="material-symbols-outlined text-[18px]">arrow_forward</span>}
            </button>
          </form>
        </div>

        <div className="text-center mt-lg">
          <p className="font-body-md text-body-md text-on-surface-variant">
            Already have an account? 
            <Link to="/login" className="text-primary hover:text-primary-container font-semibold transition-colors ml-1">Login</Link>
          </p>
        </div>
      </main>
    </div>
  );
}