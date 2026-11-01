import { forwardRef } from 'react';
import { Slot } from '@radix-ui/react-slot';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';

const buttonVariants = cva(
  // Base comum: o focus-visible é obrigatório aqui — o portal é navegado por
  // teclado e leitores de tela, e o outline padrão some com o reset do Tailwind.
  'inline-flex items-center justify-center gap-2 rounded-md font-medium transition-colors ' +
    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 focus-visible:ring-offset-2 ' +
    'disabled:pointer-events-none disabled:opacity-60',
  {
    variants: {
      variant: {
        primary: 'bg-brand-600 text-white hover:bg-brand-700',
        // Receber dinheiro e confirmar chegada de mercadoria: verde escuro, que
        // mantém o texto branco legível (o green-600 fica em 3,3:1).
        success: 'bg-green-700 text-white hover:bg-green-800',
        danger: 'bg-red-700 text-white hover:bg-red-800',
        'danger-outline':
          'border border-red-300 text-red-700 hover:bg-red-50 ' +
          'dark:border-red-500/40 dark:text-red-400 dark:hover:bg-red-900/20',
        secondary:
          'bg-brand-100 text-brand-800 hover:bg-brand-200 ' +
          'dark:bg-brand-900 dark:text-brand-100 dark:hover:bg-brand-800',
        ghost:
          'text-brand-800 hover:bg-brand-50 dark:text-brand-100 dark:hover:bg-white/10',
        outline:
          'border border-gray-300 text-gray-800 hover:bg-gray-50 ' +
          'dark:border-white/20 dark:text-white dark:hover:bg-white/10',
      },
      // Abaixo de md os botões têm 44px de altura, o mínimo confortável ao toque.
      size: {
        sm: 'h-11 px-3 text-sm md:h-9',
        md: 'h-11 px-4 text-sm md:h-10',
        lg: 'h-12 px-6 text-base',
      },
    },
    defaultVariants: { variant: 'primary', size: 'md' },
  }
);

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  /** Renderiza o filho no lugar do &lt;button&gt; — use para um Link com cara de botão. */
  asChild?: boolean;
}

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, asChild = false, ...props }, ref) => {
    const Comp = asChild ? Slot : 'button';
    return (
      <Comp className={cn(buttonVariants({ variant, size }), className)} ref={ref} {...props} />
    );
  }
);
Button.displayName = 'Button';
