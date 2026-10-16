import { Suspense, useEffect, useRef, useState } from 'react';
import { Moon, Sun } from 'lucide-react';
import {
  ALL_ENTRIES,
  DESIGN_SYSTEM,
  NAV_GROUPS,
  OVERVIEW_ENTRY,
  type NavGroup,
} from './registry';

function readHashId(): string {
  const id = new URLSearchParams(window.location.hash.slice(1)).get('page');
  if (!id) {
    return OVERVIEW_ENTRY.id;
  }
  return ALL_ENTRIES.some((entry) => entry.id === id)
    ? id
    : OVERVIEW_ENTRY.id;
}

function useSelectedId(): [string, (id: string) => void] {
  const [selected, setSelected] = useState(readHashId);

  useEffect(() => {
    const onHashChange = () => setSelected(readHashId());
    window.addEventListener('hashchange', onHashChange);
    return () => window.removeEventListener('hashchange', onHashChange);
  }, []);

  const select = (id: string) => {
    setSelected(id);
    window.location.hash = new URLSearchParams({ page: id }).toString();
  };

  return [selected, select];
}

function NavigationItems({
  groups,
  activeId,
  select,
}: {
  groups: NavGroup[];
  activeId: string;
  select: (id: string) => void;
}) {
  return (
    <nav aria-label="Navigation du système de design" className="space-y-5 py-2">
      <button
        type="button"
        onClick={() => select(OVERVIEW_ENTRY.id)}
        aria-current={OVERVIEW_ENTRY.id === activeId}
        className="block w-full rounded-md px-2 py-2 text-left text-sm font-medium transition-colors hover:bg-muted aria-[current=true]:bg-primary aria-[current=true]:text-primary-foreground"
      >
        {OVERVIEW_ENTRY.name}
      </button>

      {groups.map((group) => (
        <div key={group.name}>
          <p className="px-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
            {group.name}
          </p>
          <div className="mt-2 space-y-1 border-l pl-2">
            {group.entries.map((entry) => (
              <button
                key={entry.id}
                type="button"
                onClick={() => select(entry.id)}
                aria-current={entry.id === activeId}
                className="block w-full rounded-md px-2 py-1.5 text-left text-sm transition-colors hover:bg-muted aria-[current=true]:bg-primary aria-[current=true]:text-primary-foreground"
              >
                {entry.name}
              </button>
            ))}
          </div>
        </div>
      ))}
    </nav>
  );
}

export function DesignSystemBrowser() {
  const [selectedId, select] = useSelectedId();
  const [isDark, setIsDark] = useState(true);
  const mobileNav = useRef<HTMLDetailsElement>(null);
  const mobileNavSummary = useRef<HTMLElement>(null);
  const active =
    ALL_ENTRIES.find((entry) => entry.id === selectedId) ?? OVERVIEW_ENTRY;
  const activeGroup = NAV_GROUPS.find((group) =>
    group.entries.some((entry) => entry.id === active.id),
  );
  const ActivePage = active.Page;

  useEffect(() => {
    window.scrollTo({ top: 0 });
  }, [active.id]);

  const selectPage = (id: string) => {
    select(id);
    if (mobileNav.current?.open) {
      mobileNav.current.removeAttribute('open');
      mobileNavSummary.current?.focus();
    }
  };

  return (
    <div
      className={`${isDark ? 'dark' : ''} min-h-screen bg-background text-foreground md:grid md:grid-cols-[260px_minmax(0,1fr)]`}
    >
      <aside className="border-b border-sidebar-border bg-sidebar/95 md:sticky md:top-0 md:flex md:h-screen md:flex-col md:border-b-0 md:border-r">
        <div className="flex items-center gap-3 border-b border-sidebar-border px-5 py-5">
          <img
            alt=""
            className="size-10 rounded-xl shadow-[0_7px_20px_hsl(var(--primary)/0.24)]"
            src={`${import.meta.env.BASE_URL}gameecobot-mark.png`}
          />
          <div className="min-w-0">
            <p className="text-sm font-medium">GameEcoBot</p>
            <p className="mt-1 text-xs text-muted-foreground">Système de design</p>
          </div>
        </div>
        <div className="hidden min-h-0 flex-1 overflow-y-auto px-4 py-4 md:block">
          <NavigationItems
            groups={NAV_GROUPS}
            activeId={active.id}
            select={selectPage}
          />
        </div>
        <details ref={mobileNav} className="border-t px-4 py-3 md:hidden">
          <summary
            ref={mobileNavSummary}
            className="cursor-pointer text-sm font-medium"
          >
            Parcourir les sections :{' '}
            <span className="text-muted-foreground">{active.name}</span>
          </summary>
          <div className="mt-3 h-64 overflow-y-auto pb-2">
            <NavigationItems
              groups={NAV_GROUPS}
              activeId={active.id}
              select={selectPage}
            />
          </div>
        </details>
      </aside>

      <main className="min-w-0 px-6 py-10 sm:px-10 lg:px-14">
        <div className="mx-auto max-w-5xl">
          <header className="border-b border-border/70 pb-8">
            <div className="mb-6 flex items-center justify-between gap-4">
              <div className="flex items-center gap-2 text-xs font-medium uppercase tracking-[0.18em] text-muted-foreground">
                <span className="size-1.5 rounded-full bg-primary shadow-[0_0_12px_hsl(var(--primary)/0.8)]" />
                Aperçu interactif
              </div>
              <button
                aria-label={isDark ? 'Activer le thème clair' : 'Activer le thème sombre'}
                className="inline-flex min-h-10 items-center gap-2 rounded-lg border border-border bg-card/70 px-3 text-xs font-medium text-foreground transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                onClick={() => setIsDark((dark) => !dark)}
                type="button"
              >
                {isDark ? <Sun size={15} /> : <Moon size={15} />}
                {isDark ? 'Thème clair' : 'Thème sombre'}
              </button>
            </div>
            {active.id === OVERVIEW_ENTRY.id ? (
              <div className="flex items-start gap-4">
                <img
                  alt=""
                  className="hidden size-14 rounded-2xl shadow-[0_10px_24px_hsl(var(--primary)/0.22)] sm:block"
                  src={`${import.meta.env.BASE_URL}gameecobot-mark.png`}
                />
                <div>
                  <h1 className="text-3xl font-medium tracking-tight sm:text-4xl">
                    {DESIGN_SYSTEM.title}
                  </h1>
                  <p className="mt-3 max-w-2xl text-sm leading-relaxed text-muted-foreground sm:text-base">
                    {DESIGN_SYSTEM.description}
                  </p>
                </div>
              </div>
            ) : (
              <>
                <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                  {activeGroup?.name}
                </p>
                <h1 className="mt-2 text-2xl font-medium">{active.name}</h1>
                <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
                  {active.description}
                </p>
              </>
            )}
          </header>

          <div className="pt-8">
            <Suspense
              fallback={
                <div
                  role="status"
                  className="rounded-xl border bg-card p-6 text-sm text-muted-foreground"
                >
                  Chargement de l’aperçu…
                </div>
              }
            >
              <ActivePage />
            </Suspense>
          </div>
        </div>
      </main>
    </div>
  );
}
