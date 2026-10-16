# Consuming GameEcoBot Design System in web apps

Read `artifacts/gameecobot-design-system/docs/AGENTS.md` first. This guide covers
React/Vite and other shadcn/Tailwind web consumers. If the app already contains
a local theme or component library, also read
`artifacts/gameecobot-design-system/docs/migrating-web.md` before writing UI.

## Theme

Import this package's theme once from the app's main CSS:

```css
@import "@workspace/gameecobot-design-system/styles.css";
```

`styles.css` already imports Tailwind, its plugins, and this package's token
theme. It also registers this package's component sources. Do not add a separate
Tailwind import or a `node_modules` source path in a Tailwind v4 consumer.
Tailwind v3 consumers keep their existing `@tailwind` directives and add
`node_modules/@workspace/gameecobot-design-system/src/components` to `content`.

## Components and helpers

Import the provided controls and `cn` directly from this package:

```tsx
import { ModernButton } from "@workspace/gameecobot-design-system/components/ModernButton";
import { ModernCard } from "@workspace/gameecobot-design-system/components/ModernCard";
import { cn } from "@workspace/gameecobot-design-system/lib/utils";
```

Use the package component whenever it provides the required family. Keep
product-specific compositions in the app, but compose them from package
primitives rather than recreating those primitives locally.

Notification behavior remains app-owned; this system does not ship a toast
provider.

## Verify

After wiring the workspace dependency, import and render one of the available
`Modern*` components. Run the app's typecheck and dev server. The import must
resolve and the component must use this package's theme before broader UI work
begins.

## Ongoing rules

- Keep one source of theme variables.
- Import package-provided primitives and helpers from the package path.
- Add reusable product-agnostic components to this package first.
- For a non-shadcn app, use the tokens as the source of truth and adapt existing
  components to the token CSS variables without copying token values.
- The source GameEcoBot app is WinForms; its C# code cannot import these React
  components. Translate the same tokens and interaction states into its native
  drawing controls when adapting the desktop app.
