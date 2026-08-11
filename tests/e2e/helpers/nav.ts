import { Page, expect } from '@playwright/test';

/** Orgnr for Digitaliseringsdirektoratet — finnes alltid i seed-data */
export const ORGNR = '991825827';
export const BASE = `/${ORGNR}`;

/** Prefix for testdata — gjør det lett å finne og rydde opp */
export const TP = '[E2E]';

/** Naviger innen tenant-konteksten og vent på at Blazor har hydrert */
export async function goto(page: Page, path: string) {
  await page.goto(`${BASE}${path}`);
  await page.waitForLoadState('domcontentloaded');
}

/**
 * Slett en malgruppe fra liste-siden.
 * Malgruppe-rader har en "Slett"-knapp direkte i listen.
 */
export async function slettMalgruppe(page: Page, navn: string) {
  const rad = page.locator('.card-row', { hasText: navn }).first();
  await rad.getByRole('button', { name: 'Slett' }).click();
  // Blazor kan vise bekreftelsesdialog — håndter begge tilfeller
  const dialog = page.getByRole('dialog');
  const harDialog = await dialog.isVisible({ timeout: 1_500 }).catch(() => false);
  if (harDialog) {
    await dialog.getByRole('button', { name: /bekreft|ja/i }).click();
  }
}

/**
 * Slett en abonnementsliste fra detalj-siden.
 * Listen har ikke Slett på oversikten — man må inn på detalj.
 */
export async function slettAbonnementsliste(page: Page, navn: string) {
  await page.getByText(navn).first().click();
  await expect(page).toHaveURL(/abonnenter\/\d+/, { timeout: 10_000 });
  await page.getByRole('button', { name: 'Slett liste' }).click();
  await expect(page).toHaveURL(/abonnenter$/, { timeout: 10_000 });
}
