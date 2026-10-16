/* GENERATED FROM tokens.json -- DO NOT EDIT. Run scripts/build-tokens.mjs. */
// Portable design tokens (colors as hex). Web consumes the theme via
// src/index.css; mobile (Expo) and any other platform import this object so the
// whole product shares one source of truth.
export const tokens = {
  "color": {
    "light": {
      "background": "#f5f2fb",
      "foreground": "#17112c",
      "border": "#d9d0e7",
      "card": "#ffffff",
      "cardForeground": "#17112c",
      "popover": "#ffffff",
      "popoverForeground": "#17112c",
      "primary": "#6d28d9",
      "primaryForeground": "#ffffff",
      "secondary": "#eee8f7",
      "secondaryForeground": "#30264a",
      "muted": "#eee9f5",
      "mutedForeground": "#655a7a",
      "accent": "#f0e4ff",
      "accentForeground": "#54249a",
      "destructive": "#be123c",
      "destructiveForeground": "#ffffff",
      "input": "#cec3df",
      "ring": "#7c3aed",
      "chart1": "#6d28d9",
      "chart2": "#9333ea",
      "chart3": "#a855f7",
      "chart4": "#c084fc",
      "chart5": "#4f46e5",
      "sidebar": "#f1edfa",
      "sidebarForeground": "#30264a",
      "sidebarBorder": "#d9d0e7",
      "sidebarPrimary": "#6d28d9",
      "sidebarPrimaryForeground": "#ffffff",
      "sidebarAccent": "#e8def5",
      "sidebarAccentForeground": "#30264a",
      "sidebarRing": "#7c3aed"
    },
    "dark": {
      "background": "#0c0a18",
      "foreground": "#f5f3ff",
      "border": "#3c3069",
      "card": "#16112c",
      "cardForeground": "#f5f3ff",
      "popover": "#130f24",
      "popoverForeground": "#f5f3ff",
      "primary": "#7c3aed",
      "primaryForeground": "#ffffff",
      "secondary": "#241b3a",
      "secondaryForeground": "#eee8ff",
      "muted": "#211a35",
      "mutedForeground": "#b5a8d3",
      "accent": "#a855f7",
      "accentForeground": "#ffffff",
      "destructive": "#e11d48",
      "destructiveForeground": "#ffffff",
      "input": "#342a5a",
      "ring": "#c084fc",
      "chart1": "#7c3aed",
      "chart2": "#a855f7",
      "chart3": "#c084fc",
      "chart4": "#d8b4fe",
      "chart5": "#6d28d9",
      "sidebar": "#0a0812",
      "sidebarForeground": "#f5f3ff",
      "sidebarBorder": "#282040",
      "sidebarPrimary": "#7c3aed",
      "sidebarPrimaryForeground": "#ffffff",
      "sidebarAccent": "#1c1530",
      "sidebarAccentForeground": "#f5f3ff",
      "sidebarRing": "#c084fc"
    }
  },
  "fontFamily": {
    "sans": [
      "Ash",
      "Segoe UI",
      "sans-serif"
    ],
    "serif": [
      "Georgia",
      "serif"
    ],
    "mono": [
      "Cascadia Code",
      "Consolas",
      "monospace"
    ]
  },
  "radius": "0.75rem",
  "spacing": "0.25rem"
} as const;

export type Tokens = typeof tokens;
export default tokens;
