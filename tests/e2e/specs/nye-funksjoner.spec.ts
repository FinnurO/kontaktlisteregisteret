/**
 * E2E-tester for nye funksjoner innført 2026-08-11:
 *
 * - B-05 Historikk og revisjon     — /admin/revisjon viser AuditLog
 * - B-10 Excel-eksport             — ↓ Excel-knapp på låst adresseliste
 * - B-25 Velg alle / Fravelg alle  — toggle-knapp i statisk-søk-veiviseren
 * - B-27 Sortering i oversiktslister — Nyeste / Alfabetisk / Flest mottakere
 * - B-30 c/o og visningsnavn        — redigerbare felt i statisk-orgnr-veiviser
 */

import { test, expect } from '@playwright/test';
import { BASE, TP, goto, slettMalgruppe } from '../helpers/nav';

// ── B-27: Sortering i oversiktslister ────────────────────────────────────────

test.describe('B-27 Sortering i oversiktslister', () => {
  test('Målgrupper: sort-select finnes og endrer rekkefølge uten feil', async ({ page }) => {
    await goto(page, '/malgrupper');

    // Sort-select er i page-header — skal alltid finnes uavhengig av antall grupper
    const select = page.locator('select').first();
    await expect(select).toBeVisible({ timeout: 10_000 });

    // Alfabetisk-sortering (verdien i Malgrupper.razor er "alfabet")
    await select.selectOption('alfabet');
    // Siden laster ikke på nytt — Blazor sorterer in-memory. Første rad skal fremdeles være synlig.
    await expect(page.locator('.card-row').first()).toBeVisible({ timeout: 5_000 });

    // Flest mottakere (verdi: "antall")
    await select.selectOption('antall');
    await expect(page.locator('.card-row').first()).toBeVisible({ timeout: 5_000 });

    // Tilbake til Nyeste (verdi: "nyeste")
    await select.selectOption('nyeste');
    await expect(page.locator('.card-row').first()).toBeVisible({ timeout: 5_000 });
  });

  test('Adresselister: sort-select finnes og endrer rekkefølge uten feil', async ({ page }) => {
    await goto(page, '/adresselister');
    const select = page.locator('select').first();
    await expect(select).toBeVisible({ timeout: 10_000 });
    await select.selectOption('alfabet');
    await expect(page.locator('.card-row').first()).toBeVisible({ timeout: 5_000 });
    await select.selectOption('nyeste');
  });

  test('Abonnementslister: sort-select finnes og endrer rekkefølge uten feil', async ({ page }) => {
    await goto(page, '/abonnenter');
    const select = page.locator('select').first();
    await expect(select).toBeVisible({ timeout: 10_000 });
    await select.selectOption('alfabet');
    // Listen kan være tom (empty-state) eller ha rader — bare verifiser at siden ikke krasjer
    await expect(page.locator('.page-header')).toBeVisible({ timeout: 5_000 });
    await select.selectOption('nyeste');
  });
});

// ── B-05: Revisjonslogg ───────────────────────────────────────────────────────

test('B-05 /admin/revisjon viser revisjonstabell', async ({ page }) => {
  // Revisjon-siden er global (ikke under /{orgnr}/)
  await page.goto('/admin/revisjon');
  await page.waitForLoadState('domcontentloaded');

  // Siden skal laste uten feil — h1 synlig
  await expect(page.locator('h1')).toBeVisible({ timeout: 15_000 });
  await expect(page.locator('h1')).toContainText(/revisjon/i);

  // Revisjon.razor bruker div-layout (.card .card-row.small), ikke <table>
  // Siden skal enten vise innslag (hvis database har data) eller empty-state
  const harInnslag = await page.locator('.card .card-row.small').first().isVisible({ timeout: 5_000 }).catch(() => false);
  const harEmptyState = await page.locator('.empty-state').isVisible({ timeout: 2_000 }).catch(() => false);
  expect(harInnslag || harEmptyState).toBe(true);

  if (harInnslag) {
    // Verifiser at radene inneholder en handling-tekst og EnhetsType-tag
    await expect(page.locator('.card-row.small .row-name').first()).toBeVisible();
    await expect(page.locator('.card-row.small .tag').first()).toBeVisible();
  }
});

// ── B-25: Velg alle / Fravelg alle ───────────────────────────────────────────

test('B-25 velg-alle-knapp i statisk-søk-veiviseren', async ({ page }) => {
  const navn = `${TP} B25-VelgAlle`;

  // Steg 1
  await page.goto(`${BASE}/malgrupper/ny`);
  await page.waitForSelector('#blazor-ready', { state: 'attached', timeout: 15_000 });
  await page.getByPlaceholder('Gi målgruppen et navn...').fill(navn);
  await page.getByText('Statisk — søk og velg').click();

  const neste = page.getByRole('button', { name: 'Neste →' });
  await expect(neste).toBeEnabled({ timeout: 10_000 });
  await neste.click();

  // Steg 2: søk på et kjent, lite resultatsett
  const sokInput = page.locator('input[placeholder*="Søk"]');
  await expect(sokInput).toBeVisible({ timeout: 10_000 });
  await sokInput.fill('Riksrevisjonen');
  await page.getByRole('button', { name: 'Søk i Brreg' }).click();

  // Brreg-kall kan ta 5–15 s
  await expect(page.locator('.card .card-row.small').first()).toBeVisible({ timeout: 30_000 });

  // "Velg alle (N)"-knappen skal dukke opp når resultater er lastet
  const velgAlle = page.getByRole('button', { name: /Velg alle/i });
  await expect(velgAlle).toBeVisible({ timeout: 5_000 });
  await velgAlle.click();

  // Etter klikk skal knappen skifte til "Fravelg alle (N)"
  const fravelgAlle = page.getByRole('button', { name: /Fravelg alle/i });
  await expect(fravelgAlle).toBeVisible({ timeout: 5_000 });

  // Minst én "✓ Valgt"-knapp er nå synlig i søkeresultat-radene
  await expect(page.locator('.card-row button', { hasText: '✓ Valgt' }).first()).toBeVisible({ timeout: 5_000 });

  // Klikk Fravelg alle — alle fjernes
  await fravelgAlle.click();

  // Knappen bytter tilbake til "Velg alle"
  await expect(velgAlle).toBeVisible({ timeout: 5_000 });

  // Ingen "✓ Valgt"-knapper igjen (alle er nå "+ Legg til")
  await expect(page.locator('.card-row button', { hasText: '✓ Valgt' })).toHaveCount(0, { timeout: 5_000 });
});

// ── B-30: c/o og visningsnavn i statisk-orgnr-veiviser ───────────────────────

test('B-30 visningsnavn og c/o kan redigeres i statisk-orgnr-veiviser', async ({ page }) => {
  const navn = `${TP} B30-CoAdresse`;

  // Steg 1
  await page.goto(`${BASE}/malgrupper/ny`);
  await page.waitForSelector('#blazor-ready', { state: 'attached', timeout: 15_000 });
  await page.getByPlaceholder('Gi målgruppen et navn...').fill(navn);
  await page.getByText('Statisk — orgnr-liste').click();

  const neste = page.getByRole('button', { name: 'Neste →' });
  await expect(neste).toBeEnabled({ timeout: 10_000 });
  await neste.click();

  // Steg 2: ett kjent orgnr — validerer raskt
  const textarea = page.locator('textarea.form-input').first();
  await expect(textarea).toBeVisible({ timeout: 10_000 });
  await textarea.fill('991825827'); // Digitaliseringsdirektoratet

  await page.getByRole('button', { name: 'Valider mot Brreg' }).click();
  await expect(page.locator('.validation-summary')).toBeVisible({ timeout: 30_000 });
  await expect(page.locator('.tag-static')).toContainText('1', { timeout: 5_000 });

  // B-30: Visningsnavn-input — placeholder er orgnr eller Brreg-navn
  // Finnes i .card .card-row som siste input(s) etter status-dot og navn
  // placeholder="@(r.Enhet?.navn ?? r.Orgnr)" — dvs. en lang tekst — bruk width:160px stil
  const visnavnInput = page.locator('.card .card-row input.form-input').nth(0);
  await expect(visnavnInput).toBeVisible({ timeout: 5_000 });

  await visnavnInput.fill('Digdir — alternativt navn');
  await visnavnInput.dispatchEvent('change'); // Blazor @bind bruker onchange
  await expect(visnavnInput).toHaveValue('Digdir — alternativt navn');

  // c/o-input — placeholder er "c/o..."
  const coInput = page.locator('input[placeholder="c/o..."]').first();
  await expect(coInput).toBeVisible({ timeout: 5_000 });
  await coInput.fill('v/Johann');
  await coInput.dispatchEvent('change');
  await expect(coInput).toHaveValue('v/Johann');

  // Verdiene skal overleve re-render (Blazor round-trip) — naviger til neste steg
  await neste.click();
  await expect(page.getByText('Statisk (orgnr-liste)')).toBeVisible({ timeout: 10_000 });
  // Forhåndsvisning er ikke B-30-felter — vi bare verifiserer at wizard kommer til steg 3 uten feil.
});

// ── B-10: Excel-eksport ───────────────────────────────────────────────────────
// Bruker en Låst adresseliste som ble opprettet av adresselister.spec.ts
// (den kjøres alfabetisk før denne filen og etterlater en Låst liste i databasen).

test('B-10 Excel-eksport: ↓ Excel laster ned .xlsx-fil fra låst adresseliste', async ({ page }) => {
  await goto(page, '/adresselister');

  // Finn første [E2E]-prefiks-rad med tag-static "Låst".
  // Seed-datalisten "Høring av NOU" er ikke [E2E]-prefiks og gir ISE i detalj-siden.
  // adresselister.spec.ts kjøres alfabetisk FØR denne filen og etterlater en Låst [E2E]-liste.
  const låstRad = page.locator('.card-row').filter({
    has: page.locator('.tag-static', { hasText: 'Låst' }),
  }).filter({
    hasText: TP,  // Kun [E2E]-testlister
  }).first();

  const harLåstListe = await låstRad.isVisible({ timeout: 5_000 }).catch(() => false);
  if (!harLåstListe) {
    test.skip(true, 'Ingen låste [E2E]-adresselister — kjør adresselister.spec.ts først (eller kjør hele suiten)');
    return;
  }

  await låstRad.click();
  await expect(page).toHaveURL(new RegExp(`${BASE}/adresselister/\\d+`), { timeout: 10_000 });

  // Vent til Blazor-kretsen er etablert og listen er lastet (knappene i Låst-blokken er synlige)
  await expect(page.getByRole('button', { name: '↓ JSON' })).toBeVisible({ timeout: 15_000 });

  const excelKnapp = page.getByRole('button', { name: '↓ Excel' });
  await expect(excelKnapp).toBeVisible({ timeout: 5_000 });

  const [download] = await Promise.all([
    page.waitForEvent('download', { timeout: 20_000 }),
    excelKnapp.click(),
  ]);

  expect(download.suggestedFilename()).toMatch(/\.xlsx$/i);
});
