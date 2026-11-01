import * as Dialog from '@radix-ui/react-dialog';
import { X } from 'lucide-react';
import { cn } from '@/lib/utils';

const SIZES = {
  sm: 'sm:max-w-sm',
  md: 'sm:max-w-md',
  lg: 'sm:max-w-lg',
  xl: 'sm:max-w-2xl',
  '2xl': 'sm:max-w-4xl',
};

interface ModalProps {
  title: string;
  description?: string;
  onClose: () => void;
  size?: keyof typeof SIZES;
  children: React.ReactNode;
}

/**
 * Modal padrão: Esc, foco preso e clique fora vêm do Radix. No celular abre como
 * folha presa ao rodapé, onde o polegar alcança os botões. É aberto montando o
 * componente, do mesmo jeito que as telas já faziam com `{modal && ...}`.
 */
export function Modal({ title, description, onClose, size = 'md', children }: ModalProps) {
  return (
    <Dialog.Root open onOpenChange={(open) => { if (!open) onClose(); }}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-50 flex items-end justify-center bg-black/50 animate-in fade-in-0 duration-150 sm:items-center sm:p-4">
          <Dialog.Content
            {...(description ? {} : { 'aria-describedby': undefined })}
            // Sem campo com autoFocus, o foco vai para o próprio modal: o padrão
            // cairia no X, e um Enter logo depois fecharia o que acabou de abrir.
            onOpenAutoFocus={(e) => {
              e.preventDefault();
              (e.currentTarget as HTMLElement | null)?.focus();
            }}
            className={cn(
              'flex max-h-[92dvh] w-full flex-col overflow-hidden rounded-t-2xl bg-raised shadow-xl outline-none',
              'animate-in slide-in-from-bottom-4 duration-200 sm:rounded-xl sm:slide-in-from-bottom-0',
              SIZES[size]
            )}
          >
            <div className="flex shrink-0 items-start justify-between gap-3 border-b border-line px-5 py-4">
              <div className="min-w-0">
                <Dialog.Title className="font-semibold text-gray-900 dark:text-white">{title}</Dialog.Title>
                {description && (
                  <Dialog.Description className="mt-0.5 text-sm text-gray-500 dark:text-gray-400">
                    {description}
                  </Dialog.Description>
                )}
              </div>
              <Dialog.Close
                aria-label="Fechar"
                className="-m-2 flex h-11 w-11 shrink-0 items-center justify-center rounded-md text-gray-500 hover:bg-gray-100 hover:text-gray-700 dark:text-gray-400 dark:hover:bg-white/10 dark:hover:text-white md:m-0 md:h-8 md:w-8"
              >
                <X size={18} />
              </Dialog.Close>
            </div>
            {children}
          </Dialog.Content>
        </Dialog.Overlay>
      </Dialog.Portal>
    </Dialog.Root>
  );
}

/** Área rolável do modal. O título e os botões ficam sempre à vista. */
export function ModalBody({ className, children }: { className?: string; children: React.ReactNode }) {
  return <div className={cn('min-h-0 flex-1 overflow-y-auto p-5', className)}>{children}</div>;
}

export function ModalFooter({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex shrink-0 justify-end gap-2 border-t border-line px-5 pt-4 pb-[max(1rem,env(safe-area-inset-bottom))] max-sm:[&>*]:flex-1">
      {children}
    </div>
  );
}
