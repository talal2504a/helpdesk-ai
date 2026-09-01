import React from 'react';

/// Page/section level loader — design tokens ke saath
export default function Loading({ size = 28, label = 'Loading…', full = false }) {
  return (
    <div className={`flex flex-col items-center justify-center gap-sm ${full ? 'h-full min-h-[200px]' : 'py-xl'}`}>
      <div
        style={{ width: size, height: size, borderWidth: Math.max(2, Math.round(size / 10)) }}
        className="border-surface-variant border-t-primary rounded-full animate-spin"
      ></div>
      {label && <span className="font-body-sm text-body-sm text-on-surface-variant">{label}</span>}
    </div>
  );
}

/// Buttons ke andar chhota spinner — border-current hai to button ke color me dikhega
export function BtnSpinner({ size = 16 }) {
  return (
    <span
      style={{ width: size, height: size, borderWidth: 2 }}
      className="border-current border-t-transparent rounded-full animate-spin inline-block flex-shrink-0"
    ></span>
  );
}
