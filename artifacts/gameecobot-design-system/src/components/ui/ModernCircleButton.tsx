import type { ButtonHTMLAttributes, ReactNode } from 'react';

export type ModernCircleButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  label: string;
  children: ReactNode;
  size?: 'sm' | 'md' | 'lg';
  destructive?: boolean;
};

const SIZE_CLASSES = {
  sm: 'size-7 text-xs',
  md: 'size-8 text-sm',
  lg: 'size-10 text-base',
} as const;

export function ModernCircleButton({
  label,
  children,
  size = 'md',
  destructive = false,
  className = '',
  ...props
}: ModernCircleButtonProps) {
  return (
    <button
      aria-label={label}
      className={`ec-orb-button ${SIZE_CLASSES[size]} ${destructive ? 'ec-orb-button-danger' : ''} disabled:cursor-not-allowed disabled:opacity-45 ${className}`}
      type="button"
      {...props}
    >
      <span className="relative z-10">{children}</span>
    </button>
  );
}
