/**
 * E2E-tester for Målgrupper.
 *
 * Dekker:
 * - Opprett statisk målgruppe via orgnr-liste
 * - Opprett dynamisk målgruppe med Brreg-filter
 * - Opprett statisk målgruppe via navneoppslag (inkl. bekrefting av treff)
 * - Rediger målgruppenavn inline
 */

import { test, expect } from '@playwright/test';
import { BASE, TP, goto, slettMalgruppe } from '../helpers/nav';

// ── Rydd opp testdata fra en evt. tidligere avbrutt testkjøring ─────────────
test.beforeEach(async ({ page }) => {
  await goto(page, '/malgrupper');
  // Slett rader med [E2E]-prefiks som evt. ble liggende etter feilet test
  const testRader = page.locator('.card-row', { hasText: TP });
  const antall = await testRader.count();
  for (let i = antall - 1; i >= 0; i--) {
    const knapp = testRader.nth(i).getByRole('button', { name: 'Slett' });
    if (await knapp.isVisible({ timeout: 500 }).catch(() => false)) {
      await knapp.click();
    }
  }
});

// ────────────────────────────────────────────────────────────────────────────

test('opprett statisk målgruppe via orgnr-liste og slett', async ({ page }) => {
  const navn = `${TP} Statisk-Orgnr`;

  // ── Steg 1: Navn og type ─────────────────────────────────────────────────
  await goto(page, '/malgrupper/ny');

  // Vent til #blazor-ready — emittert av OnAfterRender(firstRender) — bekrefter
  // at SignalR-kretsen er etablert. Deretter er fill() deterministisk.
  await page.waitForSelector('#blazor-ready', { state: 'attached', timeout: 15_000 });
  const navnFelt = page.getByPlaceholder('Gi målgruppen et navn...');
  await navnFelt.fill(navn);

  // Klikk på label-kortets tekst for å velge type
  await page.getByText('Statisk — orgnr-liste').click();

  // Vent til "Neste →" blir klikkbar (Blazor oppdaterer CanProceed etter SignalR-roundtrip)
  const nesteKnapp = page.getByRole('button', { name: 'Neste →' });
  await expect(nesteKnapp).toBeEnabled({ timeout: 10_000 });
  await nesteKnapp.click();

  // ── Steg 2: Orgnr-liste ──────────────────────────────────────────────────
  // Tekstfeltet er en <textarea> med placeholder som inneholder eksempel-orgnr
  const orgnrInput = page.locator('textarea.form-input').first();
  await expect(orgnrInput).toBeVisible({ timeout: 10_000 });
  await orgnrInput.fill('974760843\n974761076\n971032081');

  await page.getByRole('button', { name: 'Valider mot Brreg' }).click();

  // Brreg-kall kan ta 5–15 s
  await expect(page.locator('.validation-summary')).toBeVisible({ timeout: 30_000 });
  await expect(page.locator('.tag-static')).toContainText('3', { timeout: 5_000 });

  // Visuell regresjonstest av validert tilstand (masker dynamiske datoer)
  await expect(page.locator('.wizard-body')).toHaveScreenshot('orgnr-validert.png', {
    mask: [page.locator('.muted')],
  });

  await nesteKnapp.click();

  // ── Steg 3: Forhåndsvis ──────────────────────────────────────────────────
  await expect(page.getByText('Statisk (orgnr-liste)')).toBeVisible({ timeout: 10_000 });
  // exact:true for å unngå strict mode — wstep-div inneholder også tallet "3"
  await expect(page.getByText('3', { exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'Opprett målgruppe' }).click();

  // ── Verifiser i liste ────────────────────────────────────────────────────
  await expect(page).toHaveURL(new RegExp(`${BASE}/malgrupper$`), { timeout: 15_000 });
  await expect(page.getByText(navn)).toBeVisible({ timeout: 5_000 });

  // ── Rydd opp ─────────────────────────────────────────────────────────────
  await slettMalgruppe(page, navn);
  await expect(page.getByText(navn)).not.toBeVisible({ timeout: 5_000 });
});

// ─────────────────────────────────────────────────────────────────────────────

test('opprett dynamisk målgruppe med Brreg-filter og slett', async ({ page }) => {
  const navn = `${TP} Dynamisk-KOMM`;

  await goto(page, '/malgrupper/ny');
  await page.waitForSelector('#blazor-ready', { state: 'attached', timeout: 15_000 });

  const navnFelt = page.getByPlaceholder('Gi målgruppen et navn...');
  await navnFelt.fill(navn);

  // "Dynamisk" er default — behøver ikke klikke på den
  const nesteKnapp = page.getByRole('button', { name: 'Neste →' });
  await expect(nesteKnapp).toBeEnabled({ timeout: 10_000 });
  await nesteKnapp.click();

  // ── Steg 2: Filterregler ─────────────────────────────────────────────────
  // Label har ingen for/id — bruk select-elementet direkte
  const orgFormSelect = page.locator('select.form-input').first();
  await expect(orgFormSelect).toBeVisible({ timeout: 10_000 });
  await orgFormSelect.selectOption('KOMM');

  await page.getByRole('button', { name: 'Forhåndsvis resultater' }).click();

  // Forhåndsvisning henter fra Brreg — kan ta 10–20 s
  await expect(page.locator('.preview-count')).toBeVisible({ timeout: 30_000 });
  const previewTekst = await page.locator('.preview-count').textContent() ?? '';
  // Teksten kan inneholde mer enn bare tallet (f.eks. "350 enheter") — plukk ut første siffer-sekvens
  const antall = parseInt(previewTekst.match(/\d+/)?.[0] ?? '0');
  expect(antall).toBeGreaterThan(100); // Norge har ~350 kommuner

  // Visuell regresjonstest av filter-UI (ikke lista selv, den er for lang)
  await expect(page.locator('.wizard-body .form-row').first()).toHaveScreenshot('dynamisk-filter.png');

  // Etter preview vises en ekstra "Neste →"-knapp (btn-sm) — bruk btn-primary eksplisitt for å unngå strict mode
  await page.locator('button.btn-primary', { hasText: 'Neste →' }).click();

  // ── Steg 3 ───────────────────────────────────────────────────────────────
  // exact:true for å unngå match på gruppenavnet "[E2E] Dynamisk-KOMM" som inneholder "Dynamisk"
  await expect(page.getByText('Dynamisk', { exact: true })).toBeVisible({ timeout: 10_000 });
  const chipOrgForm = page.locator('.chip', { hasText: 'Org.form: KOMM' });
  await expect(chipOrgForm).toBeVisible();

  await page.getByRole('button', { name: 'Opprett målgruppe' }).click();

  await expect(page).toHaveURL(new RegExp(`${BASE}/malgrupper$`), { timeout: 15_000 });
  await expect(page.getByText(navn)).toBeVisible();

  await slettMalgruppe(page, navn);
  await expect(page.getByText(navn)).not.toBeVisible({ timeout: 5_000 });
});

// ─────────────────────────────────────────────────────────────────────────────

test('opprett statisk målgruppe via navneoppslag og slett', async ({ page }) => {
  const navn = `${TP} Navneliste`;

  await goto(page, '/malgrupper/ny');
  await page.waitForSelector('#blazor-ready', { state: 'attached', timeout: 15_000 });

  const navnFelt = page.getByPlaceholder('Gi målgruppen et navn...');
  await navnFelt.fill(navn);

  await page.getByText('Statisk — navneliste').click();

  const nesteKnapp = page.getByRole('button', { name: 'Neste →' });
  await expect(nesteKnapp).toBeEnabled({ timeout: 10_000 });
  await nesteKnapp.click();

  // ── Steg 2: Navneoppslag ─────────────────────────────────────────────────
  const navneInput = page.locator('textarea.form-input').first();
  await expect(navneInput).toBeVisible({ timeout: 10_000 });
  // Bruk kjente, entydige navn — forventer sikkert treff (ingen ⚠)
  await navneInput.fill('Riksrevisjonen\nSkatteetaten');

  await page.getByRole('button', { name: 'Slå opp i Brreg' }).click();

  // Vent til begge treff er funnet
  await expect(page.getByText(/2 av 2 inkludert/i)).toBeVisible({ timeout: 30_000 });
  // exact:true — søkefeltet viser 'Riksrevisjonen' (brukers input), men treffet viser 'RIKSREVISJONEN' (Brreg-navn)
  await expect(page.getByText('RIKSREVISJONEN', { exact: true })).toBeVisible();
  await expect(page.getByText('SKATTEETATEN', { exact: true })).toBeVisible();

  // Ingen ⚠-symboler for disse entydige navnene
  await expect(page.locator('span[title*="Usikkert"]')).toHaveCount(0);

  // Visuell regresjonstest
  await expect(page.locator('.card')).toHaveScreenshot('navneoppslag-treff.png', {
    mask: [page.locator('.muted')],
  });

  await nesteKnapp.click();
  await expect(page.getByText('Statisk (navneliste)')).toBeVisible({ timeout: 10_000 });
  await page.getByRole('button', { name: 'Opprett målgruppe' }).click();

  await expect(page).toHaveURL(new RegExp(`${BASE}/malgrupper$`), { timeout: 15_000 });
  await expect(page.getByText(navn)).toBeVisible();

  await slettMalgruppe(page, navn);
  await expect(page.getByText(navn)).not.toBeVisible({ timeout: 5_000 });
});

// ─────────────────────────────────────────────────────────────────────────────

test('rediger målgruppenavn inline', async ({ page }) => {
  const opprinnelig = `${TP} Rename-Test`;
  const nytt = `${TP} Rename-Test Oppdatert`;

  // Opprett en minimal STATISK målgruppe (dynamisk uten kriterier henter ALLE enheter fra Brreg → timeout)
  await goto(page, '/malgrupper/ny');
  await page.waitForSelector('#blazor-ready', { state: 'attached', timeout: 15_000 });
  const navnFelt = page.getByPlaceholder('Gi målgruppen et navn...');
  await navnFelt.fill(opprinnelig);
  await page.getByText('Statisk — orgnr-liste').click();
  const nesteKnapp = page.getByRole('button', { name: 'Neste →' });
  await expect(nesteKnapp).toBeEnabled({ timeout: 10_000 });
  await nesteKnapp.click();

  // Steg 2: ett kjent orgnr — validerer raskt
  const orgnrInput = page.locator('textarea.form-input').first();
  await expect(orgnrInput).toBeVisible({ timeout: 10_000 });
  await orgnrInput.fill('974760843');
  await page.getByRole('button', { name: 'Valider mot Brreg' }).click();
  await expect(page.locator('.validation-summary')).toBeVisible({ timeout: 30_000 });
  await nesteKnapp.click();

  await expect(page.getByText('Statisk (orgnr-liste)')).toBeVisible({ timeout: 10_000 });
  await page.getByRole('button', { name: 'Opprett målgruppe' }).click();
  await expect(page).toHaveURL(new RegExp(`${BASE}/malgrupper$`), { timeout: 15_000 });

  // Gå til detalj-siden
  await page.getByText(opprinnelig).first().click();
  await expect(page).toHaveURL(new RegExp(`${BASE}/malgrupper/\\d+`), { timeout: 10_000 });

  // Rediger navn via "✎ Navn"-knappen
  await page.getByRole('button', { name: /✎.*Navn|Navn/i }).click();

  // Skriv nytt navn i tekstfeltet som dukker opp
  const navnInput = page.locator('h1 input.form-input');
  await expect(navnInput).toBeVisible({ timeout: 5_000 });
  await navnInput.fill(nytt);
  // @bind="editNavn" binds på onchange (ikke oninput) — fill() dispatcher ikke change.
  // Dispatcher change eksplisitt slik at Blazor oppdaterer editNavn på server, klikk deretter "Lagre".
  await navnInput.dispatchEvent('change');
  await page.waitForTimeout(400); // SignalR round-trip localhost: typisk <100ms, 400ms er trygt margin
  await page.getByRole('button', { name: 'Lagre' }).click();

  // Verifiser at overskriften er oppdatert
  await expect(page.locator('h1')).toContainText(nytt, { timeout: 10_000 });

  // Visuell regresjonstest av oppdatert detalj-header
  await expect(page.locator('.page-header')).toHaveScreenshot('malgruppe-navn-oppdatert.png', {
    mask: [page.locator('.muted, time')],
  });

  // Rydd opp
  await goto(page, '/malgrupper');
  await slettMalgruppe(page, nytt);
});
