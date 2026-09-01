import React, { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useAuth } from '../AuthContext.jsx';
import { api, saveSession } from '../api.js';
import { BtnSpinner } from '../components/Spinner.jsx';

export default function Login() {
  const { setUser } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const submit = async (e) => {
    e.preventDefault();
    setLoading(true); setError('');
    try {
      console.log('Login attempt:', { email, password: '***' });
      console.log('API base URL:', import.meta.env.DEV ? 'http://localhost:5000/api' : '/api');
      const { data } = await api.post('/auth/login', { email, password });
      console.log('Login success:', data);
      saveSession(data);
      setUser({ id: data.id, name: data.name, email: data.email, role: data.role, expiresAtUtc: data.expiresAtUtc });
      navigate('/');
    } catch (err) { 
      console.error('Login error:', err);
      console.error('Error response:', err.response);
      console.error('Error message:', err.message);
      setError(err.message); 
    } finally { setLoading(false); }
  };

  return (
    <div className="min-h-screen bg-surface flex items-center justify-center p-md">
      <main className="w-full max-w-[400px] bg-surface-container-lowest rounded-xl border border-outline-variant/30 p-xl shadow-level-2 flex flex-col items-center anim-pop">
        <div className="mb-lg flex flex-col items-center">
          <div className="w-16 h-16 bg-primary rounded-xl mb-sm flex items-center justify-center anim-floaty anim-wiggle-hover cursor-pointer">
            <span className="material-symbols-outlined text-on-primary text-[32px] fill">support_agent</span>
          </div>
          <h1 className="font-headline-lg text-headline-lg text-primary text-center">Welcome Back</h1>
          <p className="font-body-sm text-body-sm text-on-surface-variant text-center mt-xs">Sign in to your agent workspace</p>
        </div>
        
        <form className="w-full flex flex-col gap-md" onSubmit={submit}>
          <div className="relative">
            <span className="material-symbols-outlined absolute left-md top-1/2 -translate-y-1/2 text-on-surface-variant">mail</span>
            <input 
              className="w-full bg-surface-container-lowest border border-outline-variant rounded-lg py-sm pl-10 pr-sm font-body-md text-body-md text-on-surface focus:outline-none focus:border-primary focus:ring-2 focus:ring-primary/10 transition-all"
              id="email" 
              type="email" 
              placeholder="Email Address" 
              required 
              value={email} 
              onChange={(e) => setEmail(e.target.value)} 
            />
          </div>
          
          <div className="relative">
            <span className="material-symbols-outlined absolute left-md top-1/2 -translate-y-1/2 text-on-surface-variant">lock</span>
            <input 
              className="w-full bg-surface-container-lowest border border-outline-variant rounded-lg py-sm pl-10 pr-10 font-body-md text-body-md text-on-surface focus:outline-none focus:border-primary focus:ring-2 focus:ring-primary/10 transition-all"
              id="password" 
              type={showPassword ? 'text' : 'password'} 
              placeholder="Password" 
              required 
              value={password} 
              onChange={(e) => setPassword(e.target.value)} 
            />
            <button 
              type="button"
              className="absolute right-md top-1/2 -translate-y-1/2 text-on-surface-variant hover:text-primary transition-colors"
              onClick={() => setShowPassword(!showPassword)}
            >
              <span className="material-symbols-outlined text-[20px]">
                {showPassword ? 'visibility_off' : 'visibility'}
              </span>
            </button>
          </div>

          <div className="flex items-center justify-between mt-xs mb-sm">
            <label className="flex items-center gap-xs cursor-pointer">
              <input type="checkbox" className="rounded border-outline-variant text-primary focus:ring-primary/20 bg-surface-container-lowest cursor-pointer" />
              <span className="font-body-sm text-body-sm text-on-surface-variant">Remember me</span>
            </label>
            <a href="#" className="font-body-sm text-body-sm text-primary hover:underline">Forgot password?</a>
          </div>

          {error && <div className="error">{error}</div>}

          <button 
            type="submit" 
            disabled={loading}
            className="w-full bg-primary-container text-on-primary-container font-label-md text-label-md py-md rounded-lg hover:bg-primary-container/90 transition-colors focus:outline-none focus:ring-2 focus:ring-primary/30 flex justify-center items-center gap-xs disabled:opacity-50"
          >
            {loading && <BtnSpinner />}
            {loading ? 'Signing in…' : 'Login'}
            {!loading && <span className="material-symbols-outlined text-[18px]">login</span>}
          </button>

          <div className="mt-md text-center">
            <span className="font-body-sm text-body-sm text-on-surface-variant">Don't have an account? </span>
            <Link to="/register" className="font-label-md text-label-md text-primary hover:underline">Register</Link>
          </div>
        </form>
      </main>
    </div>
  );
}