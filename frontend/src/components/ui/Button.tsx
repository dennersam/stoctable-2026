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
        primary: 'bg-brand-600 text-white hover:bg-brand-500',
        secondary:
          'bg-brand-100 text-brand-800 hover:bg-brand-200 ' +
          'dark:bg-brand-900 dark:text-brand-100 dark:hover:bg-brand-800',
        ghost:
          'text-brand-800 hover:bg-brand-50 dark:text-brand-100 dark:hover:bg-white/10',
        outline:
          'border border-brand-300 text-brand-800 hover:bg-brand-50 ' +
          'dark:border-white/20 dark:text-white dark:hover:bg-white/10',
      },
      size: {
        sm: 'h-9 px-3 text-sm',
        md: 'h-10 px-4 text-sm',
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
