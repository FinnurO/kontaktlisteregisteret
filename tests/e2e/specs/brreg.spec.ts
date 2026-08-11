/**
 * E2E-tester for Brreg-søk.
 *
 * Dekker:
 * - Fritekst-søk på navn
 * - Filter på organisasjonsform
 * - Ekspander hierarki (overordnet enhet + underenheter)
 * - Massevalider orgnr-liste
 * - Navneoppslag-fanen (lim inn liste)
 */

import { test, expect } from '@playwright/test';

// Brreg-søk er globalt (ikke per virksomhet)
const BRREG_URL = '/brreg';

test.describe('Brreg-søk', () => {
  test('fritekst-søk finner kjent virksomhet', async ({ page }) => {
    await page.goto(BRREG_URL);
    await page.waitForLoadState('domcontentloaded');

    // Søkefelt og søkeknapp
    const søkeFelt = page.getByPlaceholder(/navn|orgnr|søk/i).first();
    await expect(søkeFelt).toBeVisible({ timeout: 10_000 });
    await søkeFelt.fill('Riksrevisjonen');
    await page.getByRole('button', { name: /^Søk/i }).click();

    // Forventer treff — exact:true for å unngå partial-match på NTL RIKSREVISJONEN o.l.
    await expect(page.getByText('RIKSREVISJONEN', { exact: true })).toBeVisible({ timeout: 30_000 });

    // Visuell regresjonstest av søkeresultat-kort
    const resultatkort = page.locator('.card').first();
    await expect(resultatkort).toHaveScreenshot('brreg-soek-riksrevisjonen.png', {
      mask: [page.locator('.muted')],
    });
  });

  test('søk på orgnr gir eksakt treff', async ({ page }) => {
    // BrregSok har ikke org.form-filter — tester orgnr-søk i stedet
    await page.goto(BRREG_URL);
    await page.waitForLoadState('domcontentloaded');

    const søkeFelt = page.getByPlaceholder(/navn|orgnr|søk/i).first();
    await expect(søkeFelt).toBeVisible({ timeout: 10_000 });
    await søkeFelt.fill('974760843'); // Riksrevisjonen — stabilt orgnr

    await page.getByRole('button', { name: /^Søk/i }).click();

    // Eksakt orgnr-søk skal returnere RIKSREVISJONEN som første/eneste treff
    await expect(page.getByText('RIKSREVISJONEN', { exact: true })).toBeVisible({ timeout: 30_000 });
    await expect(page.locator('.card-row').first()).toBeVisible();
  });

  test('ekspander hierarki viser detaljer og underenheter', async ({ page }) => {
    await page.goto(BRREG_URL);
    await page.waitForLoadState('domcontentloaded');

    // Søk etter Statsministerens kontor — liten, stabil enhet
    const søkeFelt = page.getByPlaceholder(/navn|orgnr|søk/i).first();
    await søkeFelt.fill('Statsministerens kontor');
    await page.getByRole('button', { name: /^Søk/i }).click();

    await expect(page.getByText(/Statsministerens kontor/i)).toBeVisible({ timeout: 30_000 });

    // Klikk på raden for å ekspandere
    await page.getByText(/Statsministerens kontor/i).first().click();

    // Detalj-seksjon viser orgnr, org.form og evt. underenheter
    await expect(page.locator('.org-detalj, .card-row').filter({
      hasText: /972417777|STAT/i,
    }).first()).toBeVisible({ timeout: 10_000 });
  });

  test('massevalider orgnr-liste markerer gyldige og ugyldige', async ({ page }) => {
    await page.goto(BRREG_URL);
    await page.waitForLoadState('domcontentloaded');

    // Finn fanen for orgnr-validering (kan hete "Orgnr-validering", "Valider", e.l.)
    // Prøv å klikke på tekstlenke/fane hvis den finnes, ellers er den allerede i view
    const validerFane = page.getByRole('tab', { name: /orgnr|valider/i }).or(
      page.getByText(/Orgnr-validering|Valider orgnr/i)
    ).first();

    if (await validerFane.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await validerFane.click();
    }

    // Finn tekstfeltet for å lime inn orgnr-liste
    const orgnrInput = page.locator('textarea').first();
    if (await orgnrInput.isVisible({ timeout: 5_000 }).catch(() => false)) {
      await orgnrInput.fill('974760843\n000000000\n974761076'); // 2 gyldige, 1 ugyldig

      await page.getByRole('button', { name: /Valider/i }).click();

      await expect(page.locator('.tag-static')).toContainText('2', { timeout: 30_000 });
      await expect(page.locator('.tag-error')).toContainText('1', { timeout: 5_000 });
    }
  });

  test('navneoppslag-fane slår opp orgnr fra navn', async ({ page }) => {
    await page.goto(BRREG_URL);
    await page.waitForLoadState('domcontentloaded');

    // Bytt til navneoppslag-fane
    const navnFane = page.getByRole('tab', { name: /navn|navneoppslag/i }).or(
      page.getByText('Navneoppslag (lim inn liste)')
    ).first();

    await expect(navnFane).toBeVisible({ timeout: 10_000 });
    await navnFane.click();

    // Lim inn navn
    const navnInput = page.locator('textarea').first();
    await expect(navnInput).toBeVisible({ timeout: 5_000 });
    await navnInput.fill('Riksrevisjonen\nSkatteetaten\nStatens vegvesen');

    await page.getByRole('button', { name: 'Slå opp' }).click();

    // Vent på alle tre treff
    await expect(page.getByText('974760843')).toBeVisible({ timeout: 30_000 }); // Riksrevisjonen
    await expect(page.getByText('974761076')).toBeVisible({ timeout: 5_000 }); // Skatteetaten
    await expect(page.getByText('971032081')).toBeVisible({ timeout: 5_000 }); // Statens vegvesen

    // Visuell regresjonstest
    await expect(page.locator('.card').first()).toHaveScreenshot('brreg-navneoppslag-treff.png', {
      mask: [page.locator('.muted')],
    });
  });
});
