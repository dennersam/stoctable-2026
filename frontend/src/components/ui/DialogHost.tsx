import { useState } from 'react';
import { Button } from '@/components/ui/Button';
import { Field } from '@/components/ui/Field';
import { fieldClass } from '@/components/ui/fieldClass';
import { Modal, ModalBody, ModalFooter } from '@/components/ui/Modal';
import { useDialogStore, type DialogOptions } from '@/lib/dialogs';

function DialogContent({ options, settle }: { options: DialogOptions; settle: (result: string | null) => void }) {
  const [value, setValue] = useState('');
  const { title, description, confirmLabel, cancelLabel = 'Voltar', tone = 'default', input } = options;
  const blocked = Boolean(input?.required) && !value.trim();

  return (
    <Modal title={title} onClose={() => settle(null)}>
      <form
        className="flex min-h-0 flex-1 flex-col"
        onSubmit={(e) => {
          e.preventDefault();
          if (!blocked) settle(value.trim());
        }}
      >
        {(description || input) && (
          <ModalBody className="space-y-4">
            {description && (
              <p className="whitespace-pre-line text-sm text-gray-600 dark:text-gray-300">{description}</p>
            )}
            {input && (
              <Field label={input.label} required={input.required}>
                <textarea
                  autoFocus
                  rows={3}
                  value={value}
                  onChange={(e) => setValue(e.target.value)}
                  placeholder={input.placeholder}
                  className={`${fieldClass} resize-none`}
                />
              </Field>
            )}
          </ModalBody>
        )}
        <ModalFooter>
          <Button type="button" variant="outline" onClick={() => settle(null)}>{cancelLabel}</Button>
          <Button type="submit" variant={tone === 'danger' ? 'danger' : 'primary'} disabled={blocked}>
            {confirmLabel}
          </Button>
        </ModalFooter>
      </form>
    </Modal>
  );
}

/** Montado uma vez no App; é quem desenha o que confirmDialog e promptDialog pedem. */
export function DialogHost() {
  const pending = useDialogStore((s) => s.pending);
  const settle = useDialogStore((s) => s.settle);

  if (!pending) return null;
  return <DialogContent key={pending.title} options={pending} settle={settle} />;
}
