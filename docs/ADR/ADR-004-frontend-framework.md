# ADR-004: Frontend Framework Selection

**Date:** 2026-02-18  
**Status:** Accepted  
**Deciders:** Architecture Team

## Context

We need a modern JavaScript framework for the CowetaConnect SPA. Primary concerns: developer experience, ecosystem maturity, suitability for a community directory/calendar app, and long-term maintainability by a small team.

## Decision

**Vue.js 3** with Composition API, Vite, Pinia, Vue Router, and Tailwind CSS.

## Rationale

- **Gentler learning curve** than React for developers new to component-based JS — important for a small team or future community contributors
- **Excellent documentation** — Vue's official docs are consistently rated best-in-class
- **Composition API** provides React Hooks-like power without JSX complexity
- **Pinia** is the official Vue state management library; lightweight and TypeScript-friendly
- **Vite** for extremely fast dev server and optimized production builds
- **Tailwind CSS** for rapid, consistent, mobile-first UI development without writing custom CSS
- Vue 3 has strong long-term support — backed by Evan You and the broader community

## Alternatives Considered

| Alternative | Reason Rejected |
|---|---|
| React | Steeper ecosystem fragmentation; more boilerplate; no strong preference given team skill set |
| Next.js (React SSR) | SSR adds server complexity; SEO not critical enough to justify (listings are indexable via meta tags) |
| Angular | Heavier framework; overkill for this application size |
| Nuxt.js (Vue SSR) | Could revisit in Phase 4 if SEO becomes critical; unnecessary complexity for Phase 1-3 |
| Svelte | Smaller ecosystem; fewer ready-made component libraries |

## Consequences

- Component library: Headless UI + Tailwind custom components (no Vuetify dependency lock-in)
- Map integration: Leaflet.js with vue-leaflet wrapper OR MapBox GL JS (decision pending API cost comparison)
- Charts: Chart.js via vue-chartjs for the owner analytics dashboard
- TypeScript used throughout — `<script setup lang="ts">` in all components
