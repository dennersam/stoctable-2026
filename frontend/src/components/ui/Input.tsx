import { forwardRef, useId } from 'react';
import { cn } from '@/lib/utils';
import { fieldClass } from './fieldClass';

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  /** Mensagem de erro. Quando presente, marca o campo como inválido para leitores de tela. */
  error?: string;
  /** Texto auxiliar, escondido automaticamente quando há erro. */
  hint?: string;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(
  ({ className, label, error, hint, id, ...props }, ref) => {
    const generatedId = useId();
    const inputId = id ?? generatedId;
    const describedById = error ? `${inputId}-error` : hint ? `${inputId}-hint` : undefined;

    return (
      <div className="w-full">
        {label && (
          <label
            htmlFor={inputId}
            className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-200"
          >
            {label}
          </label>
        )}

        <input
          id={inputId}
          ref={ref}
          aria-invalid={error ? true : undefined}
          aria-describedby={describedById}
          className={cn(
            fieldClass,
            error && 'border-red-500 focus:ring-red-500 dark:border-red-400',
            className
          )}
          {...props}
        />

        {error ? (
          <p id={`${inputId}-error`} className="mt-1.5 text-sm text-red-600 dark:text-red-400">
            {error}
          </p>
        ) : hint ? (
          <p id={`${inputId}-hint`} className="mt-1.5 text-sm text-gray-500 dark:text-gray-400">
            {hint}
          </p>
        ) : null}
      </div>
    );
  }
);
Input.displayName = 'Input';
