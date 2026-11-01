import { Search, X } from 'lucide-react';
import { cn } from '@/lib/utils';
import { fieldClass } from './fieldClass';

/**
 * Rótulo + campo. O <label> envolve o campo, então tocar no texto foca o campo
 * sem precisar de id em cada formulário.
 */
export function Field({ label, required, hint, className, children }: {
  label: string;
  required?: boolean;
  hint?: string;
  className?: string;
  children: React.ReactNode;
}) {
  return (
    <label className={cn('block', className)}>
      <span className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
        {label} {required && <span className="text-red-600 dark:text-red-400">*</span>}
      </span>
      {children}
      {hint && <span className="mt-1 block text-xs text-gray-500 dark:text-gray-400">{hint}</span>}
    </label>
  );
}

/** Título de grupo dentro de um formulário longo. */
export function FormSection({ title, children }: { title: React.ReactNode; children: React.ReactNode }) {
  return (
    <section className="space-y-4 border-t border-line pt-5 first:border-t-0 first:pt-0">
      <h3 className="text-sm font-semibold text-gray-900 dark:text-white">{title}</h3>
      {children}
    </section>
  );
}

export function SearchField({ value, onChange, placeholder, className }: {
  value: string;
  onChange: (value: string) => void;
  placeholder: string;
  className?: string;
}) {
  return (
    <div className={cn('relative', className)}>
      <Search size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-gray-500 dark:text-gray-400" />
      <input
        type="text"
        inputMode="search"
        enterKeyHint="search"
        autoComplete="off"
        aria-label={placeholder}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        className={cn(fieldClass, 'pl-9 pr-10')}
      />
      {value && (
        <button
          type="button"
          aria-label="Limpar busca"
          onClick={() => onChange('')}
          className="absolute right-0 top-0 flex h-full w-10 items-center justify-center text-gray-500 hover:text-gray-800 dark:text-gray-400 dark:hover:text-white"
        >
          <X size={16} />
        </button>
      )}
    </div>
  );
}
