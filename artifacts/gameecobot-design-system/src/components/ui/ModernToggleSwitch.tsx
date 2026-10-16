import { useState } from 'react';

export type ModernToggleSwitchProps = {
  label: string;
  description?: string;
  checked?: boolean;
  defaultChecked?: boolean;
  disabled?: boolean;
  onCheckedChange?: (checked: boolean) => void;
};

export function ModernToggleSwitch({
  label,
  description,
  checked,
  defaultChecked = false,
  disabled = false,
  onCheckedChange,
}: ModernToggleSwitchProps) {
  const [uncontrolledChecked, setUncontrolledChecked] = useState(defaultChecked);
  const isChecked = checked ?? uncontrolledChecked;

  const toggle = () => {
    if (disabled) {
      return;
    }

    const nextValue = !isChecked;
    if (checked === undefined) {
      setUncontrolledChecked(nextValue);
    }
    onCheckedChange?.(nextValue);
  };

  return (
    <div className="flex items-center justify-between gap-4">
      <div className="min-w-0">
        <p className="text-sm font-medium text-foreground">{label}</p>
        {description ? (
          <p className="mt-0.5 text-xs leading-relaxed text-muted-foreground">
            {description}
          </p>
        ) : null}
      </div>
      <button
        aria-checked={isChecked}
        aria-label={label}
        className="ec-switch-track focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-45"
        data-checked={isChecked}
        disabled={disabled}
        onClick={toggle}
        role="switch"
        type="button"
      >
        <span className="ec-switch-thumb" />
      </button>
    </div>
  );
}
