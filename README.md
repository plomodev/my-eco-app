# GameEcoBot

This repository contains the GameEcoBot Windows application and the supporting Replit workspace packages.

## Projects

- `windows-app` — native Windows x64 application built with .NET 10 and Windows Forms.
- `artifacts/gameecobot-design-system` — GameEcoBot visual design system.
- `artifacts/api-server` — API service.
- `artifacts/mockup-sandbox` — isolated UI component preview app.
- `lib` — shared API, database, and client packages.

## Build the Windows application

```powershell
dotnet publish windows-app/GameEcoBot.csproj -c Release -r win-x64 --self-contained true -o generated-artifacts/GameEcoBot-win-x64
```

The published executable is attached to GitHub Releases. Generated build output is not committed to the source repository.

## Build the workspace packages

Install pnpm, then run:

```sh
pnpm install
pnpm run build
```
