import type { HTMLAttributes, ReactNode } from 'react';

export type ModernCardProps = HTMLAttributes<HTMLDivElement> & {
  eyebrow?: string;
  title?: string;
  description?: string;
  trailing?: ReactNode;
};

export function ModernCard({
  eyebrow,
  title,
  description,
  trailing,
  children,
  className = '',
  ...props
}: ModernCardProps) {
  return (
    <div className={`ec-surface p-5 text-card-foreground ${className}`} {...props}>
      {eyebrow || title || description || trailing ? (
        <div className="relative z-10 mb-4 flex items-start justify-between gap-4">
          <div className="min-w-0">
            {eyebrow ? (
              <p className="text-[11px] font-medium uppercase tracking-[0.18em] text-muted-foreground">
                {eyebrow}
              </p>
            ) : null}
            {title ? (
              <h3 className="mt-1 text-base font-medium tracking-tight">
                {title}
              </h3>
            ) : null}
            {description ? (
              <p className="mt-1 text-sm leading-relaxed text-muted-foreground">
                {description}
              </p>
            ) : null}
          </div>
          {trailing ? <div className="shrink-0">{trailing}</div> : null}
        </div>
      ) : null}
      <div className="relative z-10">{children}</div>
    </div>
  );
}
