/**
 * E2E-tester for Abonnementslister.
 *
 * Dekker:
 * - Opprett abonnementsliste via innebygd skjema
 * - Legg til gyldig abonnent
 * - Valider avvisning av ugyldig e-post
 * - Slett enkeltabonnent
 * - Slett hele abonnementslisten
 */

import { test, expect } from '@playwright/test';
import { BASE, TP, goto, slettAbonnementsliste } from '../helpers/nav';

const LISTE_NAVN = `${TP} Abonnementsliste`;
const GYLDIG_EPOST = 'e2e-test@digdir.example.no';
const UGYLDIG_EPOST = 'dette-er-ikke-en-epost';

test.describe('Abonnementslister', () => {
  // Rydd opp testdata fra evt. tidligere avbrutt kjøring
  test.beforeEach(async ({ page }) => {
    await goto(page, '/abonnenter');
    const gammel = page.getByText(LISTE_NAVN).first();
    if (await gammel.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await slettAbonnementsliste(page, LISTE_NAVN);
    }
  });

  // ─────────────────────────────────────────────────────────────────────────

  test('opprett abonnementsliste via skjema', async ({ page }) => {
    await goto(page, '/abonnenter');

    // Vis opprettskjema — Blazor SignalR trenger tid etter navigasjon, bruk generøs timeout
    await page.getByRole('button', { name: '+ Ny abonnementsliste' }).click();
    await expect(page.getByPlaceholder(/Høring av NOU/i)).toBeVisible({ timeout: 15_000 });

    // Fyll inn navn
    await page.getByPlaceholder(/Høring av NOU/i).fill(LISTE_NAVN);
    await page.getByRole('button', { name: 'Opprett liste' }).click();

    // Redirect til detalj-siden
    await expect(page).toHaveURL(new RegExp(`${BASE}/abonnenter/\\d+`), { timeout: 15_000 });
    await expect(page.locator('h1')).toContainText(LISTE_NAVN, { timeout: 5_000 });

    // Visuell regresjonstest av ny, tom abonnementsliste
    await expect(page.locator('.page-header')).toHaveScreenshot('abonnementsliste-ny.png', {
      mask: [page.locator('.muted, time, strong')],
    });
  });

  test('legg til gyldig abonnent', async ({ page }) => {
    // Opprett liste
    await goto(page, '/abonnenter');
    await page.getByRole('button', { name: '+ Ny abonnementsliste' }).click();
    await page.getByPlaceholder(/Høring av NOU/i).fill(LISTE_NAVN);
    await page.getByRole('button', { name: 'Opprett liste' }).click();
    await expect(page).toHaveURL(new RegExp(`${BASE}/abonnenter/\\d+`), { timeout: 15_000 });

    // Legg til e-postadresse
    const epostInput = page.getByPlaceholder('epost@eksempel.no');
    await epostInput.fill(GYLDIG_EPOST);
    await page.getByRole('button', { name: '+ Legg til' }).click();

    // Abonnenten dukker opp i listen
    await expect(page.getByText(GYLDIG_EPOST)).toBeVisible({ timeout: 10_000 });

    // Abonnent-teller i header oppdateres
    const tag = page.locator('.page-header .tag');
    await expect(tag).toContainText('1', { timeout: 5_000 });

    // Visuell regresjonstest
    await expect(page.locator('.card').last()).toHaveScreenshot('abonnementsliste-med-abonnent.png', {
      mask: [page.locator('.muted, time')],
    });

    // Rydd opp
    await slettAbonnementsliste(page, LISTE_NAVN);
  });

  test('ugyldig e-post gir feilmelding', async ({ page }) => {
    // Opprett liste
    await goto(page, '/abonnenter');
    await page.getByRole('button', { name: '+ Ny abonnementsliste' }).click();
    await page.getByPlaceholder(/Høring av NOU/i).fill(LISTE_NAVN);
    await page.getByRole('button', { name: 'Opprett liste' }).click();
    await expect(page).toHaveURL(new RegExp(`${BASE}/abonnenter/\\d+`), { timeout: 15_000 });

    // Prøv ugyldig e-post
    const epostInput = page.getByPlaceholder('epost@eksempel.no');
    await epostInput.fill(UGYLDIG_EPOST);
    await page.getByRole('button', { name: '+ Legg til' }).click();

    // Feilmeldingen skal vises
    await expect(page.locator('.form-error')).toBeVisible({ timeout: 10_000 });
    // Abonnenten skal IKKE legges til
    await expect(page.getByText(UGYLDIG_EPOST)).not.toBeVisible({ timeout: 3_000 });

    // Visuell regresjonstest av feil-tilstand
    await expect(page.locator('.card').first()).toHaveScreenshot('abonnement-ugyldig-epost.png');

    // Rydd opp
    await slettAbonnementsliste(page, LISTE_NAVN);
  });

  test('slett enkeltabonnent', async ({ page }) => {
    // Opprett liste og legg til abonnent
    await goto(page, '/abonnenter');
    await page.getByRole('button', { name: '+ Ny abonnementsliste' }).click();
    await page.getByPlaceholder(/Høring av NOU/i).fill(LISTE_NAVN);
    await page.getByRole('button', { name: 'Opprett liste' }).click();
    await expect(page).toHaveURL(new RegExp(`${BASE}/abonnenter/\\d+`), { timeout: 15_000 });

    await page.getByPlaceholder('epost@eksempel.no').fill(GYLDIG_EPOST);
    await page.getByRole('button', { name: '+ Legg til' }).click();
    await expect(page.getByText(GYLDIG_EPOST)).toBeVisible({ timeout: 10_000 });

    // Slett abonnenten
    const abonnentRad = page.locator('.card-row', { hasText: GYLDIG_EPOST }).first();
    await abonnentRad.getByRole('button', { name: /Slett|✕/i }).click();

    // Abonnenten er borte
    await expect(page.getByText(GYLDIG_EPOST)).not.toBeVisible({ timeout: 10_000 });
    // Teller er tilbake til 0
    await expect(page.locator('.page-header .tag')).toContainText('0', { timeout: 5_000 });

    // Rydd opp
    await slettAbonnementsliste(page, LISTE_NAVN);
  });
});
