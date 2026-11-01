import { cn } from '@/lib/utils';

type DivProps = React.HTMLAttributes<HTMLDivElement>;

export function Card({ className, ...props }: DivProps) {
  return (
    <div
      className={cn(
        'rounded-xl border border-line bg-surface p-6',
        className
      )}
      {...props}
    />
  );
}

export function CardTitle({ className, ...props }: React.HTMLAttributes<HTMLHeadingElement>) {
  return (
    <h3
      className={cn('text-lg font-semibold text-gray-900 dark:text-white', className)}
      {...props}
    />
  );
}

export function CardDescription({ className, ...props }: React.HTMLAttributes<HTMLParagraphElement>) {
  return (
    <p
      className={cn('mt-2 text-sm leading-relaxed text-gray-600 dark:text-gray-300', className)}
      {...props}
    />
  );
}
