/**
 * Playwright Global Setup — Server-oppvarming
 *
 * Kjøres én gang FØR alle tester. Navigerer til wizard-siden og venter til
 * Blazor-kretsen er etablert. Dette varmer opp .NET JIT, EF Core og SignalR
 * slik at den første testen ikke møter en kald server.
 *
 * Uten oppvarming: første navigasjon til /malgrupper/ny kan ta 2–8 s (JIT),
 * og Blazor-kretsen er ikke etablert innen fill()-kallet, noe som gjør at
 * oninput-hendelsen faller bort og "Neste →"-knappen forblir deaktivert.
 */

import { chromium, expect, type FullConfig } from '@playwright/test';

const WARMUP_URL = 'http://localhost:5100/991825827/malgrupper/ny';

export default async function globalSetup(_config: FullConfig) {
  console.log('\n[global-setup] Varmer opp server og Blazor-krets...');

  const browser = await chromium.launch();
  const page = await browser.newPage();

  try {
    await page.goto(WARMUP_URL, { waitUntil: 'load', timeout: 60_000 });

    // Fyll inn navn for å trigge oninput → Blazor oppdaterer name-variabelen
    const navnFelt = page.getByPlaceholder('Gi målgruppen et navn...');
    await expect(navnFelt).toBeVisible({ timeout: 30_000 });
    await navnFelt.fill('warmup');

    // Vent til Neste-knappen aktiveres — bekrefter at kretsen er oppe og oninput virker
    const nesteKnapp = page.locator('button.btn-primary', { hasText: 'Neste →' });
    await expect(nesteKnapp).toBeEnabled({ timeout: 30_000 });

    console.log('[global-setup] Server klar — Blazor-krets etablert ✓');
  } catch (e) {
    // Ikke blokker testene om oppvarmingen feiler — advarer kun
    console.warn('[global-setup] Oppvarming feilet (fortsetter likevel):', (e as Error).message);
  } finally {
    await browser.close();
  }
}
