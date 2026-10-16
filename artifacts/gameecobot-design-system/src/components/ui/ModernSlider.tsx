import { useState } from 'react';
import type { CSSProperties } from 'react';

export type ModernSliderProps = {
  label: string;
  value?: number;
  defaultValue?: number;
  min?: number;
  max?: number;
  step?: number;
  disabled?: boolean;
  onValueChange?: (value: number) => void;
};

export function ModernSlider({
  label,
  value,
  defaultValue = 100,
  min = 5,
  max = 100,
  step = 1,
  disabled = false,
  onValueChange,
}: ModernSliderProps) {
  const [uncontrolledValue, setUncontrolledValue] = useState(defaultValue);
  const currentValue = value ?? uncontrolledValue;
  const percentage = ((currentValue - min) / (max - min)) * 100;

  const updateValue = (nextValue: number) => {
    if (value === undefined) {
      setUncontrolledValue(nextValue);
    }
    onValueChange?.(nextValue);
  };

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between gap-4">
        <label className="text-sm font-medium text-foreground">{label}</label>
        <output className="rounded-md border border-border bg-muted px-2 py-1 font-mono text-xs tabular-nums text-accent-foreground">
          {currentValue}%
        </output>
      </div>
      <input
        aria-label={label}
        className="ec-slider disabled:cursor-not-allowed disabled:opacity-45"
        disabled={disabled}
        max={max}
        min={min}
        onChange={(event) => updateValue(Number(event.currentTarget.value))}
        style={{ '--slider-fill': `${percentage}%` } as CSSProperties}
        type="range"
        value={currentValue}
        step={step}
      />
      <div className="flex justify-between text-[10px] uppercase tracking-[0.15em] text-muted-foreground">
        <span>{min}%</span>
        <span>{max}%</span>
      </div>
    </div>
  );
}
