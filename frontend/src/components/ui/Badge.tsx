import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';

const badgeVariants = cva(
  'inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium',
  {
    variants: {
      variant: {
        brand: 'bg-brand-100 text-brand-800 dark:bg-brand-900 dark:text-brand-100',
        neutral: 'bg-gray-100 text-gray-700 dark:bg-white/10 dark:text-gray-200',
        success: 'bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-200',
        warning: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200',
      },
    },
    defaultVariants: { variant: 'brand' },
  }
);

export interface BadgeProps
  extends React.HTMLAttributes<HTMLSpanElement>,
    VariantProps<typeof badgeVariants> {}

export function Badge({ className, variant, ...props }: BadgeProps) {
  return <span className={cn(badgeVariants({ variant }), className)} {...props} />;
}
