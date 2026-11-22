import { expect, test } from '@playwright/test';

const USERNAME = process.env.E2E_USERNAME ?? 'admin';
const PASSWORD = process.env.E2E_PASSWORD ?? 'CgpDemo!2026';

async function login(page: import('@playwright/test').Page) {
  await page.goto('/');
  await page.locator('#username').fill(USERNAME);
  await page.locator('#password').fill(PASSWORD);
  await page.getByRole('button', { name: /Giri/i }).click();
  await expect(page.locator('.nav')).toBeVisible();
}

test.describe('Görüşme odaları, ünvanlar ve çoklu misafir', () => {
  test('web akışı: oda + katılımcı + ek misafir, detay ve rapor', async ({ page }) => {
    await login(page);

    await page.getByRole('button', { name: /Yeni gör/i }).click();
    await expect(page.locator('.modal')).toBeVisible();

    const suffix = String(Date.now()).slice(-7);
    const phone = `0555${suffix}`;
    const guestPhone = `0556${suffix}`;

    const inputs = page.locator('.modal input.input');
    await inputs.nth(0).fill('UAT');
    await inputs.nth(1).fill('Web Misafir');
    await inputs.nth(2).fill(phone);

    await page.locator('.modal input[type="date"]').fill('2027-06-15');
    await page.locator('.modal input[type="time"]').fill('09:00');

    // Katılımcı seçimi (birincil görüşmeci dışındaki ilk seçenek).
    const participants = page
      .locator('.modal .field')
      .filter({ hasText: 'Katılımcılar' })
      .locator('input[type="checkbox"]');
    if ((await participants.count()) > 0) {
      await participants.first().check();
    }

    // Ek misafir.
    await page.getByRole('button', { name: '+ Misafir ekle' }).click();
    await page.getByPlaceholder('Ad', { exact: true }).fill('Ek');
    await page.getByPlaceholder('Soyad', { exact: true }).fill('Misafir');
    await page.getByPlaceholder('Telefon', { exact: true }).fill(guestPhone);

    await page.getByRole('button', { name: 'Kaydet' }).click();

    const ignoreConflicts = page.getByRole('button', { name: /rağmen kaydet/ });
    if (await ignoreConflicts.isVisible().catch(() => false)) {
      await ignoreConflicts.click();
    }

    await expect(page.locator('.modal')).not.toBeVisible();

    await page.locator('.input.search').fill('UAT Web Misafir');
    await expect(page.getByText('UAT Web Misafir +1 misafir').first()).toBeVisible();

    await page.locator('tbody tr').first().getByRole('button').click();
    const detailModal = page.locator('.modal');
    await expect(detailModal.getByText('Misafir sonuçları')).toBeVisible();
    await expect(detailModal.locator('dt', { hasText: 'Oda' })).toBeVisible();
    await expect(detailModal.locator('dt', { hasText: 'Katılımcılar' })).toBeVisible();

    await detailModal.getByRole('button', { name: 'Kapat' }).click();

    await page.getByRole('button', { name: /Raporlar/ }).click();
    await expect(page.getByText('Oda dağılımı')).toBeVisible();

    await page.getByRole('button', { name: /İçerik Yönetimi/ }).click();
    await expect(page.getByRole('button', { name: 'Ünvanlar ve Odalar' })).toBeVisible();
    await page.getByRole('button', { name: 'Ünvanlar ve Odalar' }).click();
    await expect(page.getByText('Ünvanlar', { exact: true })).toBeVisible();
    await expect(page.getByText('Odalar', { exact: true })).toBeVisible();
  });
});
