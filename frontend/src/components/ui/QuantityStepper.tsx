import { useEffect, useRef, useState } from 'react';
import { Minus, Plus } from 'lucide-react';
import { cn } from '@/lib/utils';

const SEND_DELAY_MS = 400;

const STEP_BUTTON =
  'flex h-11 w-11 shrink-0 items-center justify-center text-gray-700 hover:bg-gray-100 disabled:opacity-40 ' +
  'md:h-8 md:w-8 dark:text-gray-200 dark:hover:bg-white/10';

interface QuantityStepperProps {
  value: number;
  label: string;
  /** Chamado depois de uma pausa, para vários toques virarem um envio só. */
  onChange: (quantity: number) => Promise<void> | void;
  /** +1 quando há envio pendente, -1 quando terminou. Quem soma sabe se pode finalizar. */
  onBusy?: (delta: 1 | -1) => void;
}

function parse(text: string) {
  return Number(text.trim().replace(',', '.'));
}

export function QuantityStepper({ value, label, onChange, onBusy }: QuantityStepperProps) {
  const [draft, setDraft] = useState(String(value));
  const [synced, setSynced] = useState(value);
  const [waiting, setWaiting] = useState(false);
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const busyRef = useRef(onBusy);

  useEffect(() => {
    busyRef.current = onBusy;
  }, [onBusy]);

  // O servidor é quem manda, exceto se a pessoa já mudou de novo: aí a resposta
  // de um envio antigo não pode apagar o que acabou de ser digitado.
  if (value !== synced) {
    setSynced(value);
    if (!waiting) setDraft(String(value));
  }

  useEffect(() => () => {
    if (timer.current !== undefined) {
      clearTimeout(timer.current);
      busyRef.current?.(-1);
    }
  }, []);

  function schedule(next: number) {
    if (timer.current === undefined) onBusy?.(1);
    else clearTimeout(timer.current);

    setWaiting(true);
    timer.current = setTimeout(async () => {
      timer.current = undefined;
      setWaiting(false);
      try {
        await onChange(next);
      } finally {
        busyRef.current?.(-1);
      }
    }, SEND_DELAY_MS);
  }

  function step(delta: number) {
    const next = (Number.isFinite(parse(draft)) ? parse(draft) : value) + delta;
    if (next <= 0) return;
    setDraft(String(next));
    schedule(next);
  }

  function handleType(text: string) {
    setDraft(text);
    const next = parse(text);
    // Campo vazio ou zero é só um estado de digitação: não envia nada.
    if (text.trim() && Number.isFinite(next) && next > 0) schedule(next);
  }

  function handleBlur() {
    const current = parse(draft);
    if (!draft.trim() || !Number.isFinite(current) || current <= 0) setDraft(String(value));
  }

  const current = parse(draft);

  return (
    <div
      role="group"
      aria-label={label}
      className="inline-flex items-center overflow-hidden rounded-md border border-gray-300 dark:border-white/15"
    >
      <button
        type="button"
        aria-label="Diminuir quantidade"
        disabled={!Number.isFinite(current) || current <= 1}
        onClick={() => step(-1)}
        className={STEP_BUTTON}
      >
        <Minus size={16} />
      </button>
      <input
        type="text"
        inputMode="decimal"
        aria-label="Quantidade"
        value={draft}
        onChange={(e) => handleType(e.target.value)}
        onBlur={handleBlur}
        onFocus={(e) => e.target.select()}
        className={cn(
          'h-11 w-12 border-x border-gray-300 bg-transparent text-center text-sm tabular-nums text-gray-900 focus:outline-none focus:ring-2 focus:ring-inset focus:ring-brand-500',
          'md:h-8 dark:border-white/15 dark:text-white'
        )}
      />
      <button type="button" aria-label="Aumentar quantidade" onClick={() => step(1)} className={STEP_BUTTON}>
        <Plus size={16} />
      </button>
    </div>
  );
}
