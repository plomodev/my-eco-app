import type { ButtonHTMLAttributes, ReactNode } from 'react';

export type ModernButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';
export type ModernButtonSize = 'sm' | 'md' | 'lg';

export type ModernButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: ModernButtonVariant;
  size?: ModernButtonSize;
  leadingIcon?: ReactNode;
};

const SIZE_CLASSES: Record<ModernButtonSize, string> = {
  sm: 'px-3 text-xs',
  md: 'px-4 text-sm',
  lg: 'px-5 text-sm',
};

export function ModernButton({
  variant = 'primary',
  size = 'md',
  leadingIcon,
  children,
  className = '',
  ...props
}: ModernButtonProps) {
  return (
    <button
      className={`ec-button inline-flex items-center justify-center gap-2 font-medium tracking-wide disabled:cursor-not-allowed disabled:opacity-45 ${SIZE_CLASSES[size]} ${className}`}
      data-variant={variant}
      type="button"
      {...props}
    >
      {leadingIcon ? <span className="relative z-10">{leadingIcon}</span> : null}
      <span className="relative z-10">{children}</span>
    </button>
  );
}
