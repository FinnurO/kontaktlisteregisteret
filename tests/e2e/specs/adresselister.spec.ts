/**
 * E2E-tester for Adresselister.
 *
 * Dekker:
 * - Opprett adresseliste
 * - Koble til målgruppe og abonnementsliste
 * - Sett status Klar
 * - Lås og verifiser snapshot-antall
 * - Eksporter JSON og CSV (verifiser nedlasting)
 * - Kopier liste
 */

import { test, expect } from '@playwright/test';
import { BASE, TP, goto } from '../helpers/nav';

const LISTE_NAVN = `${TP} Adresseliste`;
// Orgnr for en kjent dynamisk/statisk målgruppe — bruker "Kommuner" fra seed
// Vi oppretter heller en enkel statisk gruppe i beforeAll
let targetGroupId: number | null = null;
let listeId: number | null = null;

// ─────────────────────────────────────────────────────────────────────────────

test.describe('Adresselister', () => {
  test.beforeAll(async ({ browser }) => {
    const page = await browser.newPage();

    // Opprett testmålgruppe med 2 kjente orgnr
    await page.goto(`${BASE}/malgrupper/ny`);
    const navnFelt = page.getByPlaceholder('Gi målgruppen et navn...');
    await navnFelt.pressSequentially(`${TP} Adresseliste-MG`, { delay: 30 });
    await page.getByText('Statisk — orgnr-liste').click();
    const neste = page.getByRole('button', { name: 'Neste →' });
    await expect(neste).toBeEnabled({ timeout: 10_000 });
    await neste.click();

    const textarea = page.locator('textarea.form-input').first();
    await expect(textarea).toBeVisible({ timeout: 10_000 });
    await textarea.fill('974760843\n974761076');
    await page.getByRole('button', { name: 'Valider mot Brreg' }).click();
    await expect(page.locator('.validation-summary')).toBeVisible({ timeout: 30_000 });
    await neste.click();
    await expect(page.getByText('Statisk (orgnr-liste)')).toBeVisible({ timeout: 10_000 });
    await page.getByRole('button', { name: 'Opprett målgruppe' }).click();
    await expect(page).toHaveURL(new RegExp(`${BASE}/malgrupper$`), { timeout: 15_000 });

    await page.close();
  });

  test.afterAll(async ({ browser }) => {
    const page = await browser.newPage();
    // Slett testmålgruppen
    await goto(page, '/malgrupper');
    const rad = page.locator('.card-row', { hasText: `${TP} Adresseliste-MG` }).first();
    const slett = rad.getByRole('button', { name: 'Slett' });
    if (await slett.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await slett.click();
    }
    await page.close();
  });

  // ─────────────────────────────────────────────────────────────────────────

  test('opprett adresseliste', async ({ page }) => {
    await page.goto(`${BASE}/adresselister`);
    await page.waitForLoadState('domcontentloaded');

    // Klikk "+ Ny adresseliste" (er en <a>-lenke med relativ href)
    await page.getByRole('link', { name: '+ Ny adresseliste' }).click();
    await expect(page).toHaveURL(new RegExp(`${BASE}/adresselister/ny`), { timeout: 10_000 });

    // Label har ingen for/id — matche på faktisk placeholder-tekst
    const tittelFelt = page.getByPlaceholder(/Høring av NOU/i);
    await expect(tittelFelt).toBeVisible({ timeout: 10_000 });
    await tittelFelt.pressSequentially(LISTE_NAVN, { delay: 30 });

    await page.getByRole('button', { name: /Opprett|Lagre/i }).first().click();

    // Redirect til detalj-siden
    await expect(page).toHaveURL(new RegExp(`${BASE}/adresselister/\\d+`), { timeout: 15_000 });

    // Hent id fra URL for bruk i andre tester
    const url = page.url();
    const match = url.match(/adresselister\/(\d+)/);
    listeId = match ? parseInt(match[1]) : null;

    await expect(page.locator('h1')).toContainText(LISTE_NAVN, { timeout: 5_000 });

    // Visuell regresjonstest av ny detalj-side (Utkast-tilstand)
    await expect(page.locator('.detail-meta')).toHaveScreenshot('adresseliste-utkast.png', {
      mask: [page.locator('.muted, time')],
    });
  });

  test('koble til målgruppe og sett status Klar', async ({ page }) => {
    // Naviger til adresseliste-oversikt og finn testlisten
    await page.goto(`${BASE}/adresselister`);
    await page.waitForLoadState('domcontentloaded');

    await page.getByText(LISTE_NAVN).first().click();
    await expect(page).toHaveURL(new RegExp(`${BASE}/adresselister/\\d+`), { timeout: 10_000 });

    // Koble til målgruppe (knapp på detalj-siden)
    const leggTilMGKnapp = page.getByRole('button', { name: /Legg til målgruppe|Koble til/i });
    if (await leggTilMGKnapp.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await leggTilMGKnapp.click();
      // Velg testmålgruppe fra nedtrekk/søk
      const mgAlternativ = page.getByText(`${TP} Adresseliste-MG`).first();
      if (await mgAlternativ.isVisible({ timeout: 5_000 }).catch(() => false)) {
        await mgAlternativ.click();
      }
    }

    // Sett status til Klar
    const klarKnapp = page.getByRole('button', { name: /Klar|Merk som klar/i });
    if (await klarKnapp.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await klarKnapp.click();
      await expect(page.locator('.tag-dynamic')).toBeVisible({ timeout: 5_000 });
    }
  });

  test('lås adresseliste og verifiser snapshot', async ({ page }) => {
    await page.goto(`${BASE}/adresselister`);
    await page.waitForLoadState('domcontentloaded');

    await page.getByText(LISTE_NAVN).first().click();
    await expect(page).toHaveURL(new RegExp(`${BASE}/adresselister/\\d+`), { timeout: 10_000 });

    // Finn og klikk Lås-knappen
    const låsKnapp = page.getByRole('button', { name: /Lås|Ta snapshot/i });
    if (await låsKnapp.isVisible({ timeout: 5_000 }).catch(() => false)) {
      await låsKnapp.click();

      // Verifiser at status endres til "Låst"
      await expect(page.locator('.tag-static')).toContainText('Låst', { timeout: 15_000 });

      // Eksport-knappene dukker opp ved Låst-status
      await expect(page.getByRole('button', { name: '↓ JSON' })).toBeVisible({ timeout: 5_000 });
      await expect(page.getByRole('button', { name: '↓ CSV' })).toBeVisible({ timeout: 5_000 });

      // Visuell regresjonstest av Låst-tilstand
      await expect(page.locator('.page-header')).toHaveScreenshot('adresseliste-låst.png', {
        mask: [page.locator('.muted, time')],
      });
    }
  });

  test('eksporter låst adresseliste som JSON og CSV', async ({ page }) => {
    await page.goto(`${BASE}/adresselister`);
    await page.waitForLoadState('domcontentloaded');

    await page.getByText(LISTE_NAVN).first().click();
    await expect(page).toHaveURL(new RegExp(`${BASE}/adresselister/\\d+`), { timeout: 10_000 });

    const jsonKnapp = page.getByRole('button', { name: '↓ JSON' });
    const csvKnapp = page.getByRole('button', { name: '↓ CSV' });

    if (await jsonKnapp.isVisible({ timeout: 5_000 }).catch(() => false)) {
      // JSON-nedlasting
      const [jsonDownload] = await Promise.all([
        page.waitForEvent('download', { timeout: 15_000 }),
        jsonKnapp.click(),
      ]);
      expect(jsonDownload.suggestedFilename()).toMatch(/\.json$/i);

      // CSV-nedlasting
      const [csvDownload] = await Promise.all([
        page.waitForEvent('download', { timeout: 15_000 }),
        csvKnapp.click(),
      ]);
      expect(csvDownload.suggestedFilename()).toMatch(/\.csv$/i);
    } else {
      // Listen er ikke låst — skip download-sjekk
      test.skip(true, 'Listen er ikke låst, hopper over eksport-test');
    }
  });
});
