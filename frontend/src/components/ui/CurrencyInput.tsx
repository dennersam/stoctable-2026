import { forwardRef } from 'react';
import { cn } from '@/lib/utils';

/**
 * Campo de valor em real com máscara enquanto o usuário digita.
 *
 * A digitação é por centavos, como em PDV: cada dígito entra pela direita e
 * empurra os anteriores — 1 → 0,01; 12 → 0,12; 123456 → 1.234,56. Só dígitos
 * são aceitos, então não existe estado intermediário inválido e o `value`
 * exposto para fora é sempre um número em reais pronto para ir ao backend.
 *
 * Trocamos `type="number"` por texto de propósito: number não aceita máscara,
 * mostra as setinhas de spinner e, em pt-BR, deixa o usuário digitar ponto
 * como separador decimal, o que gerava valores errados sem aviso.
 */

const decimalFormatter = new Intl.NumberFormat('pt-BR', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

/** Teto de 13 dígitos (99.999.999.999,99) — acima disso o float perde precisão. */
const MAX_DIGITS = 13;

/** Formata um valor em reais como "999.999,99" (sem o prefixo, que é visual). */
function formatCurrencyInput(value: number): string {
  return decimalFormatter.format(value);
}

export interface CurrencyInputProps
  extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange' | 'type'> {
  /** Valor em reais (ex.: 1234.56). `null`/`undefined` deixam o campo vazio. */
  value: number | null | undefined;
  /** Recebe o valor em reais já convertido. */
  onValueChange: (value: number) => void;
}

export const CurrencyInput = forwardRef<HTMLInputElement, CurrencyInputProps>(
  ({ value, onValueChange, className, disabled, ...props }, ref) => {
    const display = value == null ? '' : formatCurrencyInput(value);

    const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
      const digits = e.target.value.replace(/\D/g, '').slice(0, MAX_DIGITS);
      onValueChange(digits ? Number(digits) / 100 : 0);
    };

    // O conteúdo é reformatado a cada tecla, então uma seleção no meio do texto
    // deixaria o cursor num ponto arbitrário. Ancorar no fim mantém a digitação
    // previsível — é sempre o próximo dígito à direita.
    const moveCaretToEnd = (e: React.SyntheticEvent<HTMLInputElement>) => {
      const el = e.currentTarget;
      requestAnimationFrame(() => el.setSelectionRange(el.value.length, el.value.length));
    };

    return (
      <div className="relative w-full">
        <span
          aria-hidden
          className={cn(
            'pointer-events-none absolute inset-y-0 left-3 flex items-center text-sm',
            disabled ? 'text-gray-300 dark:text-gray-600' : 'text-gray-400 dark:text-gray-500'
          )}
        >
          R$
        </span>

        <input
          {...props}
          ref={ref}
          type="text"
          inputMode="numeric"
          disabled={disabled}
          value={display}
          onChange={handleChange}
          onFocus={moveCaretToEnd}
          onClick={moveCaretToEnd}
          placeholder={props.placeholder ?? '0,00'}
          className={cn('pl-9 text-right tabular-nums', className)}
        />
      </div>
    );
  }
);
CurrencyInput.displayName = 'CurrencyInput';
